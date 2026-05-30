# local-print-agent

Minimalni Windows lokalni print servis u C# (.NET 6) koji slusa na `127.0.0.1:4567`.

Trenutna verzija je mock implementacija: prihvata i validira print zahteve, generise `jobId` i loguje zahtev. Sledeci korak je integracija sa realnim Windows printer driverom.

## Pokretanje lokalno

```bash
dotnet run
```

Servis ce po default-u slusati na:

- `http://127.0.0.1:4567`

## Health check

```bash
curl http://127.0.0.1:4567/health
```

Ocekivani odgovor:

```json
{
  "ok": true,
  "service": "local-print-agent",
  "version": "1.0.0"
}
```

## POST /print primer

Body format:

```json
{
  "appId": "prodaja-web",
  "documentType": "racun",
  "paperSize": "A5",
  "orientation": "portrait",
  "printerName": "optional",
  "copies": 1,
  "documentBase64": "SGVsbG8gUHJpbnQ="
}
```

### curl primer

Bez API key (dev mode):

```bash
curl -X POST http://127.0.0.1:4567/print \
  -H "Content-Type: application/json" \
  -d "{\"appId\":\"prodaja-web\",\"documentType\":\"racun\",\"paperSize\":\"A5\",\"orientation\":\"portrait\",\"copies\":1,\"documentBase64\":\"SGVsbG8gUHJpbnQ=\"}"
```

Sa API key:

```bash
curl -X POST http://127.0.0.1:4567/print \
  -H "Content-Type: application/json" \
  -H "X-Print-Agent-Key: moj-tajni-kljuc" \
  -d "{\"appId\":\"prodaja-web\",\"documentType\":\"racun\",\"paperSize\":\"A5\",\"orientation\":\"portrait\",\"copies\":1,\"documentBase64\":\"SGVsbG8gUHJpbnQ=\"}"
```

### PowerShell primer

Bez API key (dev mode):

```powershell
$body = @{
  appId = "prodaja-web"
  documentType = "racun"
  paperSize = "A5"
  orientation = "portrait"
  printerName = ""
  copies = 1
  documentBase64 = "SGVsbG8gUHJpbnQ="
} | ConvertTo-Json

Invoke-RestMethod -Method Post -Uri "http://127.0.0.1:4567/print" -ContentType "application/json" -Body $body
```

Sa API key:

```powershell
$headers = @{ "X-Print-Agent-Key" = "moj-tajni-kljuc" }

Invoke-RestMethod -Method Post -Uri "http://127.0.0.1:4567/print" -ContentType "application/json" -Headers $headers -Body $body
```

## Konfiguracija (appsettings.json)

Sekcija `PrintAgent` podrzava:

- `BindAddress` (default: `127.0.0.1`)
- `Port` (default: `4567`)
- `ApiKey` (opciono)
- `AllowedApps` (lista dozvoljenih `appId` vrednosti)
- `Cors:AllowedOrigins` (lista dozvoljenih browser origin-a)

Primer:

```json
"PrintAgent": {
  "BindAddress": "127.0.0.1",
  "Port": 4567,
  "ApiKey": "",
  "AllowedApps": ["prodaja-web", "kasa-app"],
  "Cors": {
    "AllowedOrigins": [
      "http://localhost:5175",
      "http://185.99.2.164",
      "https://185.99.2.164",
      "http://157.90.163.195",
      "https://157.90.163.195"
    ]
  }
}
```

## CORS (browser pristup)

Agent sada dozvoljava browser zahteve samo za origin-e iz `PrintAgent:Cors:AllowedOrigins`.

Napomena: CORS proverava tacan origin (schema + host + port), zato po potrebi dodaj i port (npr. `http://185.99.2.164:5175`).

## Kako podesiti ApiKey

Ako postavis `ApiKey` na nepraznu vrednost, endpoint `POST /print` trazi header:

- `X-Print-Agent-Key: tvoja-vrednost`

Ako `ApiKey` nije postavljen (prazan), endpoint radi bez autentikacije (dev mode).

## Kako promeniti port

U `appsettings.json` promeni:

- `PrintAgent:Port`

Primer:

```json
"Port": 5005
```

## Napomena o stampanju

Ova verzija koristi `MockPrintService` i ne salje dokument realnom printeru. Predvidjena je kao stabilna osnova za sledeci korak: integracija sa realnim Windows printer driverom i spooler API-jem.
