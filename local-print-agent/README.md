# local-print-agent

Windows lokalni print servis u C# (.NET 6) koji slusa samo na `127.0.0.1` i prima print zahteve preko HTTP API-ja.

Podrzani mode:

- `text`
- `raw`
- `pdf` (preko SumatraPDF CLI)

## Pokretanje

```bash
dotnet run
```

Default adresa:

- `http://127.0.0.1:4567`

## Endpointi

- `GET /health`
- `GET /status`
- `GET /printers`
- `GET /config-check`
- `POST /print`

## Request model (`POST /print`)

```json
{
  "appId": "prodaja-web",
  "mode": "pdf",
  "paperSize": "A4",
  "orientation": "portrait",
  "printerName": "HP LaserJet",
  "copies": 1,
  "documentBase64": "...",
  "documentType": "racun"
}
```

Pravila:

- `appId` obavezno
- `mode` obavezno (`text|raw|pdf`)
- `paperSize` obavezno za `pdf`, opciono za `text/raw` (`A4|A5`)
- `orientation` opciono (`portrait|landscape`, default `portrait`)
- `copies` 1 do 20 (default 1)
- `documentBase64` obavezno i validan Base64
- maksimalna velicina payload-a je konfigurisana (`MaxPayloadMb`, default 20MB)

## Standardni odgovor

```json
{
  "success": true,
  "jobId": "guid",
  "mode": "pdf",
  "printerUsed": "HP LaserJet",
  "paperSize": "A4",
  "copies": 1,
  "durationMs": 240,
  "errorCode": null,
  "message": "PDF je uspesno poslat na stampu."
}
```

## ErrorCode

- `INVALID_REQUEST`
- `INVALID_BASE64`
- `PAYLOAD_TOO_LARGE`
- `PRINTER_NOT_FOUND`
- `PDF_RENDERER_NOT_FOUND`
- `PRINT_TIMEOUT`
- `PRINT_FAILED`
- `UNAUTHORIZED`
- `FORBIDDEN_APP`

## PDF stampa (SumatraPDF)

Za `mode: "pdf"`:

1. Agent dekodira Base64 u privremeni `.pdf` fajl.
2. Poziva `SumatraPDF.exe` u silent print modu.
3. Ceka zavrsetak procesa do timeout-a (`PrintTimeoutSeconds`, default 60).
4. Ako timeout istekne, proces se prekida.
5. Temp fajl se brise.

## Konfiguracija (`appsettings.json`)

```json
"PrintAgent": {
  "BindAddress": "127.0.0.1",
  "Port": 4567,
  "UseMockService": false,
  "MaxPayloadMb": 20,
  "PrintTimeoutSeconds": 60,
  "ApiKey": "",
  "AllowedApps": [],
  "Pdf": {
    "SumatraPath": "C:\\Program Files\\SumatraPDF\\SumatraPDF.exe"
  },
  "Cors": {
    "AllowedOrigins": [
      "http://localhost",
      "https://localhost",
      "http://localhost:5174",
      "http://localhost:5175",
      "http://localhost:5176"
    ]
  }
}
```

Napomena:

- Servis forsira bind na `127.0.0.1`.
- Ako je `ApiKey` postavljen, `X-Print-Agent-Key` je obavezan.
- Ako `AllowedApps` nije prazna, `appId` mora biti u listi.

## Brzi test

```bash
curl http://127.0.0.1:4567/health
```

```bash
curl http://127.0.0.1:4567/status
```

`/health` koristi aplikacija da vidi da li je servis aktivan. `/status` je objedinjena provera koja vraca i stanje PDF rendera, default printera i broj dostupnih printera.

```powershell
$body = @{
  appId = "prodaja-web"
  mode = "pdf"
  paperSize = "A4"
  orientation = "portrait"
  copies = 1
  documentBase64 = "<BASE64_PDF>"
} | ConvertTo-Json

Invoke-RestMethod -Method Post -Uri "http://127.0.0.1:4567/print" -ContentType "application/json" -Body $body
```
