# NotebookLM API 使用需求規劃

版本:1.0 · 建立日期:2026-04-23
針對:**Google NotebookLM Enterprise API**(v1alpha)
官方文件:https://docs.cloud.google.com/gemini/enterprise/notebooklm-enterprise/docs/

---

## 1. 使用目的

openCrawler 需將使用者本地收藏的文章(單篇或整個分類),上傳至 NotebookLM Enterprise 成為資料來源(source),並請 NotebookLM 產生摘要。本文件只定義**本 App 與 NotebookLM API 的互動範圍**,不涵蓋一般 UI/下載流程(請見《需求規劃.md》)。

---

## 2. 前置條件(先決需求)

### 2.1 使用者須自備

| 項目 | 內容 |
|------|------|
| GCP 專案 | 使用者需有一個**已啟用 billing**(綁信用卡或簽約付款)的 Google Cloud 專案 |
| 啟用 API | 專案需啟用 **Discovery Engine API**(NotebookLM Enterprise 所在) |
| 授權角色 | Service Account 需具備 `roles/discoveryengine.admin` 或 `roles/discoveryengine.editor` |
| 認證方式 | **Service Account JSON 金鑰檔**(由使用者在設定頁選檔或拖曳上傳) |
| 端點區域 | 支援 `us` / `eu` / `global`,由使用者於設定頁選擇(預設 `global`) |

### 2.2 計費與認證機制(釐清)

- **NotebookLM Enterprise 不使用 API Key**,**只能**用 Service Account(或 OAuth 使用者登入,v1 不做)。
- 所有 API 呼叫的費用**掛在 SA 所屬的 GCP Project** 上,與其他 GCP 服務同一張帳單。
- NotebookLM Enterprise **無穩定免費 tier**,按 notebook 儲存 / source 處理 / audio 生成用量計費,以官方 pricing 頁為準。
- **不採 OAuth 的理由**(ADR 層級):OAuth 仍需使用者有 GCP project + billing,只省去「選 JSON 檔」這步,但增加 2-3 天開發(loopback server、PKCE、token refresh)。v1 目標使用者定位為技術/開發人員,處理 JSON 不成障礙。未來若普及受阻再做 OAuth。

### 2.3 設定頁 UX 要求(降低 SA JSON 的門檻)

| 要求 | 行為 |
|------|------|
| **拖曳上傳** | 設定頁可把 JSON 檔直接拖進輸入區,自動解析並填入 `client_email` / `project_id` 預覽 |
| **一鍵測試連線** | 按鈕呼叫 `GET /notebooks:listRecentlyViewed`,結果以綠/紅狀態列回饋,錯誤訊息含建議動作 |
| **內建圖文教學** | 設定頁嵌「如何建立 SA 與下載 JSON」折疊區,含官方文件連結(GCP 主控台 → IAM 與管理 → Service Accounts) |
| **欄位預填** | 解析 JSON 後自動推測 `project_number`(從 `project_id` 反查需使用者確認或手動輸入) |

### 2.4 Gemini API(文字摘要備援)的認證

本 App 另一個外部服務 Gemini,**允許兩種獨立設定**,在同一設定頁以 tab/切換顯示:

| 模式 | 認證 | 適用 |
|------|------|------|
| **AI Studio(推薦個人)** | **API Key**(貼字串即可) | 有 Google 官方免費 tier,快速上手 |
| **Vertex AI(企業)** | Service Account JSON | 走 GCP 帳單,與 NotebookLM 同一專案 |

Gemini 設定**獨立於** NotebookLM 設定:使用者只想要「純文字 AI 摘要」時,可以**只填 Gemini API Key**,不設 NotebookLM,仍能使用摘要功能(只是沒有音訊摘要與 NotebookLM 網頁對話)。

---

## 3. 本 App 需使用的 API 端點清單

以下為 v1 將實作的 API 呼叫,**基底 URL**為:

```
https://{ENDPOINT_LOCATION}-discoveryengine.googleapis.com/v1alpha/projects/{PROJECT_NUMBER}/locations/{LOCATION}
```

上傳檔案時基底 URL 改為 `/upload/v1alpha/...`。

| # | 用途 | HTTP | 路徑 |
|---|------|------|------|
| 1 | 建立 notebook | POST | `/notebooks` |
| 2 | 列出最近 notebook | GET | `/notebooks:listRecentlyViewed` |
| 3 | 取得 notebook 詳情 | GET | `/notebooks/{NOTEBOOK_ID}` |
| 4 | 批次刪除 notebook | POST | `/notebooks:batchDelete` |
| 5 | 批次加入 sources(URL/文字) | POST | `/notebooks/{NOTEBOOK_ID}/sources:batchCreate` |
| 6 | 上傳檔案為 source | POST | `/notebooks/{NOTEBOOK_ID}/sources:uploadFile`(上傳 URL 基底) |
| 7 | 取得 source 詳情 | GET | `/notebooks/{NOTEBOOK_ID}/sources/{SOURCE_ID}` |
| 8 | 批次刪除 source | POST | `/notebooks/{NOTEBOOK_ID}/sources:batchDelete` |
| 9 | 產生音訊摘要 | POST | `/notebooks/{NOTEBOOK_ID}/audioOverviews` |

Podcast API(`podcast-api`)與 notebook 分享 API 在 v1 **不實作**。

---

## 4. 詳細使用規格

### 4.1 認證

每個 HTTP 請求需加上 Header:

```
Authorization: Bearer {access_token}
Content-Type: application/json
```

**access_token 取得方式**:以 Service Account JSON 透過 `Google.Apis.Auth.OAuth2` 函式庫呼叫 `GoogleCredential.FromFile(path).CreateScoped("https://www.googleapis.com/auth/cloud-platform").UnderlyingCredential.GetAccessTokenForRequestAsync()`。

Token **快取於記憶體**,過期前自動 refresh;**絕不寫入檔案或日誌**。

---

### 4.2 建立 Notebook

**觸發時機**:使用者對某篇文章或某分類**第一次**執行「NotebookLM 摘要」時。若該 scope(文章 or 分類)已有對應 notebook(查 SQLite `notebooks` 表),跳過建立。

**請求**:
```http
POST /notebooks
{
  "title": "openCrawler · 技術 · .NET 9 新特性整理"
}
```

**標題命名規則**:
- 單篇文章:`openCrawler · {分類名} · {文章標題}`
- 整個分類:`openCrawler · {分類路徑} · (資料夾全集)`

**回應**(儲存 `notebookId` 至 SQLite):
```json
{
  "notebookId": "abc123...",
  "name": "projects/.../notebooks/abc123...",
  "title": "...",
  "metadata": { "userRole": "PROJECT_ROLE_OWNER", ... }
}
```

---

### 4.3 加入 Sources

**策略選擇**:每篇文章有兩種送出方式,本 App 預設採 **(B)** 理由見下:

| 方式 | 做法 | 優點 | 缺點 |
|------|------|------|------|
| (A) webContent | 傳入原始 URL,請 NotebookLM 自己抓 | 不需上傳內容 | 若原站擋爬蟲或已下架,NotebookLM 抓不到;與本地快照不一致 |
| (B) textContent **✓ 預設** | 從本地 HTML 抽出純文字後以 text 送出 | **與本地快照一致**;不受原站變動影響 | 單檔有長度限制需分段 |
| (C) uploadFile | 上傳本地 HTML 檔 | 結構最完整 | NotebookLM 處理 HTML 不穩定,建議轉 txt/md 再傳 |

**本 App 預設採 (B)**:下載網頁時同步產生 `content.txt`(由 AngleSharp 解析 `<article>`/`<main>` 或 readability 法則),送 NotebookLM 時讀這份純文字。

**請求**(批次加入,最多 50 個 source 一批,超過需分批):
```http
POST /notebooks/{NOTEBOOK_ID}/sources:batchCreate
{
  "userContents": [
    {
      "textContent": {
        "sourceName": "{文章標題}",
        "content": "{純文字內容}"
      }
    },
    {
      "textContent": {
        "sourceName": "{另一篇文章標題}",
        "content": "..."
      }
    }
  ]
}
```

**內容長度限制**:官方未明確公告,本 App 保守切到每 source **100,000 字元**;若單篇超過則在文字末尾附「...(內容已截斷)」並警告使用者。

**回應**:對每個 source 回 `sourceId`,寫入 SQLite `sources` 表,建立 article → remote source 的對應。

---

### 4.4 產生音訊摘要(含自訂 prompt)

**請求**:
```http
POST /notebooks/{NOTEBOOK_ID}/audioOverviews
{
  "sourceIds": [ { "id": "src_1" }, { "id": "src_2" } ],
  "episodeFocus": "{使用者輸入的 prompt}",
  "languageCode": "zh-TW"
}
```

**自訂 prompt 對應**:使用者在 App 輸入的 prompt 直接填入 `episodeFocus`。官方對此欄位描述是「要強調的重點」,實測接受一般中文指示句(例如:「請整理成商業決策摘要,突出風險與機會」)。

**sourceIds**:
- 單篇文章摘要 → 只傳該篇對應的 source id
- 整個分類摘要 → 省略 `sourceIds` 欄位,NotebookLM 會使用該 notebook 的全部 sources

**回應**:
```json
{
  "name": "projects/.../audioOverviews/xxx",
  "status": "AUDIO_OVERVIEW_STATUS_IN_PROGRESS"
}
```

此 API 是**非同步**的,需輪詢(poll)或透過 `GET /notebooks/{id}` 檢查 `audioOverview` 狀態。

**輪詢策略**:
- 首次輪詢 5 秒後
- 之後每 10 秒輪詢一次
- 最長等 5 分鐘,超時顯示「產生時間較長,請稍後至 NotebookLM 查看」並把 notebook URL 給使用者
- 完成後狀態為 `AUDIO_OVERVIEW_STATUS_READY`,同時帶音訊 URL

---

### 4.5 文字摘要(由 Gemini API 補足)

> 🚨 **重要**:NotebookLM Enterprise v1alpha **未提供**純文字 Q&A/摘要的 REST API。本 App 為滿足使用者「看文字摘要」需求,改呼叫 **Gemini API**(同個 GCP 專案)。

**端點**:
```
POST https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-pro:generateContent
```

(若使用者偏好 Vertex AI 端點可在設定中切換,v1 採 Generative Language API 較簡單)

**請求**:
```json
{
  "contents": [{
    "parts": [
      { "text": "{使用者自訂 prompt}\n\n---\n\n以下是文章內容:\n{純文字內容}" }
    ]
  }],
  "generationConfig": {
    "temperature": 0.3,
    "maxOutputTokens": 8192
  }
}
```

**結果儲存**:寫入 SQLite `summaries` 表,kind='text'。

---

## 5. 非同步與進度回饋

| 操作 | 預期耗時 | UI 呈現 |
|------|----------|---------|
| 建立 notebook | < 3s | spinner + 「建立 NotebookLM 筆記本中...」 |
| 加入 sources(單篇) | 3-10s | 「上傳至 NotebookLM...」|
| 加入 sources(批次 20 篇) | 30-90s | 進度條 `n/total` |
| 產生音訊摘要 | 1-5 分鐘 | 「產生音訊摘要中,可繼續使用 App」(背景執行) |
| Gemini 文字摘要 | 5-20s | spinner |

所有 API 呼叫皆用 `async/await`,UI 執行緒不阻塞。

---

## 6. 錯誤處理

| HTTP 狀態 | 情境 | 處理 |
|-----------|------|------|
| 401 Unauthorized | Token 過期/無效 | 自動 refresh token 重試一次,再失敗則提示使用者重設定 JSON 金鑰 |
| 403 Forbidden | Service Account 缺角色 | 顯示「權限不足,請加上 Discovery Engine Admin 角色」並附官方文件連結 |
| 404 Not Found | notebook 已被刪除 | 清除本地 SQLite 對應記錄,提示重新建立 |
| 429 Too Many Requests | Rate limit | 指數退避(exponential backoff),最多 3 次 |
| 5xx | 伺服器錯誤 | 最多重試 2 次,失敗後保留操作紀錄讓使用者手動重試 |
| 網路斷線 | | 偵測到後立即中止,不無限等待 |

所有錯誤訊息寫入 `<儲存根>/logs/notebooklm.log`(rotate 7 天),**不記錄 access token 或 JSON 金鑰內容**。

---

## 7. 本 App 封裝的服務介面(C#)

```csharp
public interface INotebookLmService
{
    Task<string> CreateNotebookAsync(string title, CancellationToken ct);
    Task<IReadOnlyList<string>> AddTextSourcesAsync(
        string notebookId,
        IEnumerable<(string name, string content)> sources,
        CancellationToken ct);
    Task<AudioOverviewResult> GenerateAudioOverviewAsync(
        string notebookId,
        IEnumerable<string>? sourceIds,
        string episodeFocus,
        string languageCode,
        CancellationToken ct);
    Task<string> WaitForAudioOverviewAsync(
        string notebookId,
        TimeSpan timeout,
        IProgress<string>? progress,
        CancellationToken ct);
}

public interface IGeminiSummaryService
{
    Task<string> SummarizeAsync(
        string prompt,
        string articleText,
        CancellationToken ct);
}
```

實作採 `HttpClientFactory` + `Polly` 重試策略,Service Account 認證走 `Google.Apis.Auth` NuGet 套件。

---

## 8. 測試計畫(與 NotebookLM API 相關)

| 項目 | 驗收方式 |
|------|----------|
| 連線測試 | 設定頁按「測試連線」,呼叫 `listRecentlyViewed`,成功/失敗皆有明確訊息 |
| 建立 notebook | API 呼叫後,可在 NotebookLM 網頁(https://notebooklm.cloud.google/)看到新 notebook |
| 加入 text source | 網頁上看到對應 source,點開內容與本地 `content.txt` 一致 |
| 長內容切斷 | 送一篇 > 100k 字元文章,驗證未觸發 API 錯誤且 UI 有警告 |
| 音訊摘要 | 輪詢後能取得可播放的音訊連結,網頁播放正常 |
| 自訂 prompt | 以中英文 prompt 各測一次,確認音訊內容反映重點 |
| 錯誤情境 | 故意填錯 project number → 收到明確錯誤訊息 |
| 金鑰過期 | 停用 Service Account → 下次呼叫應優雅失敗 |

---

## 9. 未來擴充(v2+)

- 串接 Podcast API 產生雙人對話型 Podcast
- 串接 Notebook Share API 讓使用者一鍵把 notebook 分享給同事
- 若 NotebookLM 推出文字 chat API,改用官方端點取代 Gemini 備援
- 支援從 App 內重播/下載音訊摘要(而非僅給連結)

---

## 10. 附錄:快速對照表

| 使用者動作 | 觸發的 API 呼叫 |
|-----------|----------------|
| 點「測試連線」 | `GET /notebooks:listRecentlyViewed` |
| 對單篇文章摘要(首次) | `POST /notebooks` → `POST /sources:batchCreate` → `POST /audioOverviews` |
| 對單篇文章摘要(已有 notebook) | `POST /audioOverviews`(sourceIds 指定該篇) |
| 對整個分類摘要(首次) | `POST /notebooks` → `POST /sources:batchCreate`(批次分頁) → `POST /audioOverviews`(不帶 sourceIds) |
| 刪除分類且使用者選「同時刪 NotebookLM」 | `POST /notebooks:batchDelete` |
| 點「純文字摘要」 | Gemini `generateContent`(**非** NotebookLM API) |
