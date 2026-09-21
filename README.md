# Entity Screening API

REST API en .NET 8 que busca entidades por nombre en listas de alto riesgo (sanciones y listas de vigilancia) mediante web scraping. Devuelve el número de coincidencias y un arreglo con los resultados y sus atributos.

La fuente implementada es OFAC - Sanctions List Search (Departamento del Tesoro de EE. UU.). La arquitectura permite agregar otras fuentes implementando una interfaz.

## Requisitos

- .NET 8 SDK
- Conexión a internet

## Ejecución local

```bash
dotnet build
dotnet run --project src/EntityScreening.Api --urls "http://localhost:5080"
```

Disponible en:

- API: `http://localhost:5080`
- Swagger: `http://localhost:5080/swagger`
- Health check: `http://localhost:5080/health`

## Endpoints

### GET /api/search

Busca una entidad por nombre. Requiere el header `X-API-Key`.

| Parámetro | Obligatorio | Descripción |
|---|---|---|
| `name` | Sí | Nombre de la entidad (2 a 200 caracteres). |
| `source` | No | Clave de la fuente. Por defecto `ofac`. |

Respuesta:

```json
{
  "source": "ofac",
  "query": "cuba",
  "hits": 14,
  "retrievedAtUtc": "2026-09-21T02:45:00Z",
  "results": [
    {
      "name": "AMISTUR CUBA SA",
      "attributes": {
        "Name": "AMISTUR CUBA SA",
        "Address": "Calle 13 #504 e/ D y E; Vedado",
        "Type": "Entity",
        "Program(s)": "CUBA-EO14404",
        "List": "SDN",
        "Score": "100",
        "DetailsUrl": "https://sanctionssearch.ofac.treas.gov/Details.aspx?id=57906"
      }
    }
  ]
}
```

### GET /api/sources

Devuelve las fuentes registradas. Requiere `X-API-Key`.

### GET /health

Health check público.

## Ejemplos

```bash
# Búsqueda en OFAC
curl "http://localhost:5080/api/search?name=cuba&source=ofac" -H "X-API-Key: dev-local-api-key-change-me"

# Sin source (usa ofac por defecto)
curl "http://localhost:5080/api/search?name=bank" -H "X-API-Key: dev-local-api-key-change-me"

# Listar fuentes
curl "http://localhost:5080/api/sources" -H "X-API-Key: dev-local-api-key-change-me"
```

## Autenticación

Las rutas bajo `/api` requieren el header `X-API-Key`. La clave se toma de la variable de entorno `API_KEY` o, si no existe, de `ApiKey` en `appsettings.json`. La clave local por defecto es `dev-local-api-key-change-me`; cámbiela en producción con la variable de entorno.

```bash
# Windows PowerShell
$env:API_KEY = "mi-clave-secreta"
```

## Rate limiting

Máximo 20 solicitudes por minuto por API Key (o por IP si no hay clave). Al superarlo responde 429 con el header `Retry-After`.

## Errores

Formato de respuesta:

```json
{
  "status": 400,
  "error": "validation_error",
  "message": "La solicitud contiene parámetros inválidos.",
  "details": ["El parámetro 'name' es obligatorio."]
}
```

| Código | error | Cuándo |
|---|---|---|
| 400 | validation_error | Parámetros faltantes o inválidos. |
| 400 | source_not_found | La fuente no existe. |
| 401 | missing_api_key | Falta el header X-API-Key. |
| 401 | invalid_api_key | API Key inválida. |
| 429 | rate_limit_exceeded | Se superó el límite de 20 req/min. |
| 502 | source_error | Fallo al obtener datos de la fuente externa. |
| 500 | internal_error | Error interno. |

## Fuentes

| source | Fuente | Estado |
|---|---|---|
| `ofac` | OFAC - Sanctions List Search | Funcional |

Para agregar una fuente nueva: implementar `IScraperService` y registrarla en `Program.cs`.

## Colección de Postman

En `postman/`:

- `EntityScreeningAPI.postman_collection.json`
- `EntityScreeningAPI.local.postman_environment.json`

Importar ambos en Postman, seleccionar el entorno local y ejecutar los requests.
