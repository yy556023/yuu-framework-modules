# Yuu.Observability Review 待辦（2026-09-06）

> 2026-10-29 移植複查：本文件保留 2026-09-06 的 review 紀錄；外部宿主、基建與發布狀態未於本次重新驗證，請勿視為目前狀態。
> 環境名稱判定已改為不分大小寫；2026-10-29 使用者拍板：null／空字串／空白不算 local，只有名稱包含 local 才算。
> Serilog 空環境名預設用 Information，OTLP 不再將未知環境標成 local；Hosting 仍依 K2m 行為跳過空環境名的設定檔。其餘待辦未實作。

兩份 review（正確性 + SRE）的結果，merge recommendation 為 **BLOCK**。
本檔只記錄「要做什麼」，做不做、做多少由使用者決定。

**前提（好消息）**：套件目前**沒有任何呼叫端**，`grep AddYuuObservability` 全 repo 為空，
`Yuu.AuthServer` 與 `Yuu.Sample` 的 `Program.cs` 都還是 ABP 原版。以下全部都在「第一次接線之前」，
現在改的成本遠低於部署之後。

---

## A. 已親自驗證為真（不是轉述）

### A1. Correlation ID 靠宿主擺放順序決定，擺錯時完全無聲 —— CRITICAL

從 assembly 抓符號確認：

- `Volo.Abp.Core.dll`：只有 `ICorrelationIdProvider` + `DefaultCorrelationIdProvider`（AsyncLocal `currentCorrelationId`）。
  Core 不依賴 ASP.NET，**物理上不可能讀 HttpContext**。
- `Volo.Abp.AspNetCore.dll`：`AbpCorrelationIdMiddleware` / `GetCorrelationIdFromRequest` / `CheckAndSetCorrelationIdOnResponse`。
- **預設標頭名兩邊相同**：ABP 也是 `X-Correlation-Id`（字面值在 `Volo.Abp.Core.dll`）。
- 兩個宿主都呼叫了 `app.UseCorrelationId()`：
  `AuthServerHttpApiHostModule.cs:279`、`SampleHttpApiHostModule.cs:204`，都排在 `UseRouting()` 之前。

讀標頭的是 **middleware** 而非 provider；ABP 那支在沒有標頭時會自己生 GUID 並**寫回 `Request.Headers`**。
因此誰先跑誰贏，而我們的文件只寫「要在 `UseRouting()` 之前」，跟 ABP 那行落在同一區間、沒有定序。

> 修正 reviewer 的過度說法：它說「整個設計不成立」。實際上**我們的 middleware 先跑就會成立**
> （ABP 會沿用標頭裡的 TraceId）。問題是**順序沒有被強制**，且擺錯時編譯過、跑得起來、
> 稽核有值，只是那個值是不相干的 GUID。這種失效模式比壞掉更糟。

**建議修法**：不要靠標頭做隱式交握。讓 `YuuCorrelationIdMiddleware` 注入
`ICorrelationIdProvider`（介面就在已引用的 `Volo.Abp.Core`，非 ABP 宿主用 optional 注入）
並自行 `using (provider.Change(correlationId))`，同時要求宿主**移除** `app.UseCorrelationId()`。
只留一支，順序問題自然消失。

### A2. Helm 注入的環境變數，套件根本不讀 —— P0，第一次上叢集就會踩

`Yuu.Sample.Helm/charts/yuu-service/templates/deployment.yaml:42-47` 只注入
`OTEL_EXPORTER_OTLP_ENDPOINT` / `OTEL_SERVICE_NAME` / `OTEL_RESOURCE_ATTRIBUTES`；
`values/` 底下 grep `Observability` **零筆**。套件讀的是 `Observability:OtlpEndpoint`。
ASP.NET 的環境變數提供者只把 `__` 轉 `:`，不會對應過去。

精確原因是我們自己寫的守衛：`YuuObservabilityExtensions.cs` 的
`if (!string.IsNullOrWhiteSpace(options.OtlpEndpoint))`。OTel SDK **原生就讀**
`OTEL_EXPORTER_OTLP_ENDPOINT`，但因為用設定值當開關，`AddOtlpExporter()` 根本不會被呼叫。

**結果：traces / metrics / logs 三條全部不掛 exporter，Pod 全綠、Grafana 全空、零錯誤訊息。**

**建議修法**（兩個都做）：

1. 套件側：`OtlpEndpoint` 空但 `OTEL_EXPORTER_OTLP_ENDPOINT` 有值時，呼叫無參數 `AddOtlpExporter()`
   讓 SDK 自己讀；`ServiceName` 推斷鏈補上 `OTEL_SERVICE_NAME`
   （明確指定 → OTEL_SERVICE_NAME → 組件名 → unknown-service），logs 側同步。
2. Helm 側：`deployment.yaml` 補 `Observability__OtlpEndpoint` / `__ServiceName` / `__ServiceVersion`
   （`ServiceVersion` 用 `.Values.image.tag`，目前從頭到尾沒帶）。

---

## B. 套件側其餘待辦（reviewer 提出，尚未逐條驗證）

依建議修的順序排：

1. **resource attributes 沒有單一來源** —— `deployment.environment` 只有 logs 側送、traces 側沒有；
   `service.version` traces 側空字串照送、logs 側空值跳過。應抽
   `YuuResourceAttributes.Build(...)` 兩邊共用；SDK 側改 `AddService` + `AddAttributes` 補環境。
2. **`WriteToOtlpLogs` 的 serviceName 要呼叫端手動傳**，traces 讀 appsettings ——
   照文件設定 `Observability:ServiceName` 卻沒手動傳，就會 traces/logs 分裂。
   應提供吃 `IConfiguration` 的多載，讓兩邊讀同一區段。
3. **Serilog 端整個指派 `ResourceAttributes`** 會抹掉 sink 原本對 `OTEL_RESOURCE_ATTRIBUTES` 的合併 —— 改成 merge。
4. **缺 `OnBeginSuppressInstrumentation`** —— sink 自己的 OTLP 匯出會被 `AddHttpClientInstrumentation` 追蹤，
   形成遙測回饋迴圈。加 `options.OnBeginSuppressInstrumentation = SuppressInstrumentationScope.Begin;`
5. **兩個 sink 都是滿了就丟，且 SelfLog 沒開** —— 日誌遺失本身不可觀測。
   加 `SelfLog.Enable(Console.Error)`、Console 的 Async 改 `blockWhenFull: true`、
   OTLP 明確設 `BatchingOptions`（預設 QueueLimit 100000 對 1Gi limit 太大）。
6. **關機沒 flush** —— 模組文件沒提 `Log.CloseAndFlush()` 是必要的。
   應在 XML doc 的「用法」補上，或在 `OnApplicationShutdown` 呼叫。
7. **`OtlpProtocol.Grpc` 寫死** —— 指到 4318 會靜默失敗；且 traces 側用
   `exporter.Endpoint = new Uri(...)` 會把 `AppendSignalPathToEndpoint` 設成 false，
   切 http/protobuf 時不補 `/v1/traces`。應做成 options 兩邊共用。
8. **取樣器沒包 `ParentBasedSampler`** —— 忽略上游 `traceparent` 的 sampled flag，
   兩服務 ratio 不同時跨服務 trace 會出現破洞。並對 ratio 做 `[0,1]` 驗證（目前超範圍會啟動崩潰）。
9. **Console outputTemplate 不含 TraceId** —— 我們費力算出的值在 `kubectl logs` 上一個字都看不到，
   「OTLP 掛掉退回 stdout」這條備援路徑實際不可用。
10. **非 local 缺 endpoint 應 fail fast** —— 本機沒 endpoint 是刻意的，叢集上是設定錯誤。
    用現成的 `YuuEnvironmentExtensions.IsLocal()` 判斷，搭配 `AllowMissingOtlpEndpoint` 逃生門。
11. **外來 `X-Correlation-Id` 未驗證** —— 直接進日誌、response header、`AbpAuditLogs.CorrelationId`。
    可被塞 32KB 垃圾、做 log injection、或刻意與別租戶的值相同來汙染追蹤。
    應加白名單（`^[A-Za-z0-9-]{1,64}$`），並用 `.FirstOrDefault()` 而非 `.ToString()`（多值時會逗號串接）。
    是否信任外部值應是 options 上的明確開關（面向公網的 AuthServer 預設 false）。
12. **`ExcludedPaths` 漏斜線會讓整台服務一條 trace 都不產生** ——
    `StartsWithSegments` 對非 `/` 開頭會丟例外，OTel 捕獲後回傳 false（不收集）。
    應正規化補斜線 + filter 外層 try/catch 成 `return true`。
    另建議補 ABP 端點：`/Abp/ApplicationConfiguration`、`/Abp/ApplicationLocalization`、
    `/swagger`、`/.well-known`。**`/connect/token` 不要排除**（token 壽命 5 分鐘，是最高頻業務端點）。
13. **只讀 `ASPNETCORE_ENVIRONMENT`** —— `DbMigrator` 這類 console 宿主用 `DOTNET_ENVIRONMENT`。
    舊版 `IsLocal(null) == true` 會把只設 `DOTNET_ENVIRONMENT=Production` 的部署誤判為 local。
    2026-10-29 使用者決定改為 false：空環境名使用 Information，且不送出 `deployment.environment`，避免誤標 local。
    `DOTNET_ENVIRONMENT` fallback 鏈仍待補，否則該部署的環境標籤仍會缺漏。
    大小寫判定已於 2026-10-29 移植修正：`IsDev()`／`IsStag()`／`IsProd()` 使用 `OrdinalIgnoreCase`，
    `Development` 現在會被 `IsDev()` 正確辨識，並有回歸測試；上述 `DOTNET_ENVIRONMENT` fallback 仍待處理。
14. **`Configure<>` 與 `section.Get<>()` 並存** —— 宿主用 `Configure<YuuObservabilityOptions>(...)`
    對 `IOptions` 有效、對實際 OTel 管線無效。要嘛拿掉、要嘛文件寫明只在啟動期讀一次。
15. **response header 用直接指派** —— `UseExceptionHandler` 會 `Response.Clear()`，
    500 回應反而拿不到這個標頭。改用 `Response.OnStarting`。
    （順帶確認：ABP 有 `ContainsKey` 保護，**不會**產生重複 header，這半邊的疑慮不成立。）
16. **`TraceContextEnricher` 與 Serilog 4.x 原生的 `LogEvent.TraceId`/`SpanId` 重複** ——
    OTLP sink 預設 `IncludedData` 已含這兩個原生欄位，enricher 只是多帶兩個冗餘字串屬性。
    保留的唯一理由是讓 console template 能用 `{TraceId}`；若採納第 9 項就保留並在文件寫明用途。
    （早期 return 在 `Activity.Current == null` 時是**正確**的，enrich 在呼叫端執行緒完成，沒問題。）
17. **csproj 相依與「服務非 ABP 宿主」的意圖矛盾** —— 純 Serilog 消費者會被迫拉進
    ABP + ASP.NET FrameworkReference + Npgsql。可考慮拆
    `Yuu.Observability.Serilog` / `Yuu.Observability.AspNetCore`，或至少讓 `AddNpgsql()` 可關閉。
18. `AdditionalSources` 只作用於 trace，metrics 沒有對應的 `AddMeter` —— 補 `AdditionalMeters`。
19. 缺 exemplars（`SetExemplarFilter(ExemplarFilterType.TraceBased)`）——
    metrics → trace 這一跳整個斷掉，看到 P99 尖峰無法直接跳到造成它的 trace。
    需三處配合：SDK、collector 的 `enable_open_metrics`、Prometheus 的 `exemplar-storage`。
20. OTLP 沒有 headers / 認證 / 逾時設定 —— 送去 Grafana Cloud 或走 mTLS 時要改框架。
21. `deployment.environment` 在 semconv 1.27 後更名 `deployment.environment.name` —— 過渡期兩個都送。

**確認沒問題、不需要改的**：`IMiddleware` 註冊為 Transient 是正確的
（`MiddlewareFactory` 從請求 scope 解析，Singleton 才有 captive dependency 問題）。

---

## C. 基建側 P0（Yuu.Sample.GitOps / Yuu.Sample.Helm）

1. **collector 是 `mode: daemonset` 但打 ClusterIP DNS** ——
   chart 的 `service.enabled` 預設對 daemonset 是關的，該 DNS 名稱可能不存在。
   先 `helm template` 驗證。正確做法是 node-local：`hostPort: 4317` + 用 `status.hostIP` 組 endpoint。
2. **prometheus exporter 監聽 8889，但沒有 containerPort、沒有 Service、沒有 ServiceMonitor** ——
   **應用送出的 metrics 到 collector 就停在那裡，Prometheus 從來沒抓過。**
   另需開 `resource_to_telemetry_conversion`，否則 resource attributes 只會進 `target_info`，
   Grafana 上用 `service_name="..."` 過濾查不到東西。
3. **Loki 起不來** —— 缺 `schemaConfig`（chart 6.6 後已移除內建預設）、
   缺 `limits_config.allow_structured_metadata`（沒有它 OTLP 的 trace_id 無處可放，互跳直接死）、
   缺 S3 `region`、缺 IRSA annotation、`chunksCache` 預設吃 ~9.6Gi 會 Pending。
4. **三個環境的 platform Application 共用同一 namespace 與 releaseName** ——
   `destination.server` 都是 `kubernetes.default.svc`，配上 `prune + selfHeal` 會無限互相覆寫。
5. **Grafana datasource 是裸的** —— 沒有 `uid`、沒有 `derivedFields`、沒有 `tracesToLogsV2`。
   **Loki 無法跳 trace、Tempo 無法跳 log。** 這正是「資料送到了但用不起來」。
   `tracesToLogsV2.tags` 的 key 是 resource attribute、value 是 Loki label 名（點變底線），寫錯會靜默查不到。
6. **日誌會進 Loki 兩次** —— collector 開了 `presets.logsCollection`（撿 stdout），
   而我們又直送 OTLP。儲存與查詢成本翻倍，查詢時每筆訊息出現兩遍。要決定留哪一條。
7. collector exporters 沒有 `sending_queue` + `file_storage` —— Loki/Tempo 一重啟就丟資料。
   這其實是「Serilog 直送 OTLP」能被正當化的**前提**：應用端保持輕薄記憶體緩衝，
   耐久性下放到 node-local collector 的磁碟佇列。目前這個前提不成立。
8. collector daemonset 沒設 `resources`，一次尖峰能拖垮節點上其他 Pod。
9. `terminationGracePeriodSeconds` 與 `preStop` hook 缺失 —— 滾動更新必然出現一批 502。
10. `TraceSampleRatio` 預設 1.0 且 prod values 沒覆寫。
    **不建議調低頭端取樣**（會丟掉 90% 的錯誤與慢請求），正解是 collector 端 `tail_sampling`
    （錯誤全採 + 慢請求全採 + 其餘 5%），但那需要兩層拓撲（daemonset 收集 →
    `loadbalancing` exporter 按 traceID 路由 → deployment 模式的 gateway 做尾端取樣），
    因為尾端取樣要求同一條 trace 的所有 span 到達同一個 collector 實例。
    短期折衷：prod values 明確寫死一個 ratio，別讓預設值決定正式環境成本。
11. metrics 沒有套用 `ExcludedPaths` —— 探針請求會計入 `http.server.request.duration`，
    污染 RED dashboard 與 SLO。OTel .NET 的 metrics 沒有 Filter，要在 collector 用 `filter` processor。
12. 沒有任何 PrometheusRule / Alertmanager receiver。最小集合：
    collector 丟資料（`otelcol_exporter_send_failed_*`）、
    遙測消失（`absent(target_info{service_name="..."})`）、5xx 率、P99、CrashLoop。
13. dev/stag/prod 的 platform 設定一字不差 —— 無法在 stag 驗證 prod 的取樣率與 retention。

---

## D. 建議執行順序

1. 修 A1（correlation 定序）與 A2（設定鍵不匹配 + fail fast）
2. 修 B 的 1~3（resource attributes 單一來源）
3. 拿 `Yuu.AuthServer` 當白老鼠接第一條線
   （同時移除 `Program.cs` 的 `WriteTo.File("Logs/logs.txt")`：容器裡會無上限吃節點磁碟且沒人看）
4. **本機 docker compose 起 collector + Loki + Tempo + Grafana 跑一次端到端閉環**：
   打 API → Grafana 看到 trace → 從 trace 跳到該次請求的 log → 從 log 跳回 trace。
   依 `ng-rules.md`，這個閉環沒實際跑過之前，不得宣稱這套可觀測性可用。
5. 閉環驗過再回頭處理 C 的基建 P0

---

## E. 這次一併確認的其他待辦（與本 review 無關，勿遺漏）

- `Yuu.Observability` 尚未接進 `Yuu.AuthServer` 與 `Yuu.Sample`
- `yuu-framework-modules` 的 observability 工作尚未 commit
- 三個基建 repo（Terragrunt / Helm / GitOps）100 個檔案全部未 commit
- 待 push：`yuu-framework-modules` net9、`nuget-framework` develop、clouds ×3
- `yuu-framework-modules` 10.2.0 尚未發布（13 個套件要一起，lockstep）
- AuthServer / Sample 的 CORS 白名單目前是測試用的 `AllowAnyOrigin`，要復原
- clouds 缺 `logo-dark.png`，白色 logo 在亮色主題不可見（`styles.scss` 留了註解掉的區塊）
- 是否要把 `Yuu.Observability` 加到 net9 分支，尚未決定
