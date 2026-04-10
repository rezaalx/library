# LocationSharing Trips API (.NET 10)

Production-ready REST API for temporary location sharing in Trips.

## Tech Stack

- ASP.NET Core Web API (.NET 10)
- Entity Framework Core + Npgsql provider
- PostgreSQL
- Swagger/OpenAPI
- Docker Compose (API + Postgres)

## Project Structure

```text
LocationSharing.sln
docker-compose.yml
src/
  LocationSharing.Api/
    Controllers/
    Contracts/
      Requests/
      Responses/
      Validation/
    Data/
    Middleware/
    Models/
    Utilities/
    Dockerfile
    Program.cs
```

## Data Model

Implemented entities (all with `Id` + `PublicId`):

- `Member`
- `Trip`
- `TripMember` (unique `(TripId, MemberId)`)
- `MemberLocationLatest` (unique `(TripId, MemberId)`)
- `MemberLocationHistory` (index `(TripId, MemberId, RecordedAt DESC)`)

All timestamps use `DateTimeOffset`.

## Error Handling

- `400` validation errors:
  - `{ "message": "The request is invalid.", "errors": { ... } }`
- `403/404/409/500`:
  - RFC7807 style ProblemDetails: `type`, `title`, `status`, `detail`, `instance`, `traceId`
- Global exception middleware handles unexpected errors and returns safe `500`.

## Run Locally

### Option A: Docker Compose (recommended)

```bash
docker compose up --build
```

Swagger UI:

- <http://localhost:8080/swagger>

### Option B: Local dotnet + postgres

1. Start postgres.
2. Set connection string in `src/LocationSharing.Api/appsettings.json` or env variable:
   - `ConnectionStrings__DefaultConnection`
3. Run API:

```bash
dotnet run --project src/LocationSharing.Api/LocationSharing.Api.csproj
```

Swagger UI:

- <http://localhost:5186/swagger>

## EF Core Migrations

The project is migration-ready:

```bash
dotnet tool install --global dotnet-ef
dotnet ef migrations add InitialCreate --project src/LocationSharing.Api --startup-project src/LocationSharing.Api
dotnet ef database update --project src/LocationSharing.Api --startup-project src/LocationSharing.Api
```

---

## Sample API Responses

> Note: IDs/timestamps are examples.

### 1) POST `/api/members`

**Success (201):**

```json
{
  "publicId": "7f8fdab1-f786-4f2b-b50b-0dc0e0f5902d",
  "name": "Alice Johnson",
  "email": "alice@example.com",
  "displayName": "Alice",
  "imageUrl": "https://cdn.example.com/alice.jpg",
  "createdOn": "2026-03-23T10:00:00+00:00",
  "updatedOn": "2026-03-23T10:00:00+00:00"
}
```

**Error (409 duplicate email):**

```json
{
  "type": "https://httpstatuses.com/409",
  "title": "Conflict",
  "status": 409,
  "detail": "A member with this email already exists.",
  "instance": "/api/members",
  "traceId": "00-d7f2a8a9ec4bf8f80f6d3d9fa2f06da8-8b40f8140860d8a2-00"
}
```

### 2) GET `/api/members/{memberPublicId}`

**Success (200):**

```json
{
  "publicId": "7f8fdab1-f786-4f2b-b50b-0dc0e0f5902d",
  "name": "Alice Johnson",
  "email": "alice@example.com",
  "displayName": "Alice",
  "imageUrl": "https://cdn.example.com/alice.jpg",
  "createdOn": "2026-03-23T10:00:00+00:00",
  "updatedOn": "2026-03-23T10:00:00+00:00"
}
```

**Error (404):**

```json
{
  "type": "https://httpstatuses.com/404",
  "title": "Not Found",
  "status": 404,
  "detail": "Member not found.",
  "instance": "/api/members/9f0a08a2-8a8f-4dfd-8d10-20c7ac3c61c3",
  "traceId": "00-3ba5a2a2f03f0468af4b7f341dfe27a1-fef83d2462ce2d64-00"
}
```

### 3) POST `/api/trips`

**Success (201):**

```json
{
  "publicId": "8b4c2f36-2ee8-49af-a14d-579f8a6d6f4d",
  "name": "Airport Pickup",
  "title": "Terminal 3 meetup",
  "startTime": "2026-03-23T10:00:00+00:00",
  "endTime": "2026-03-23T13:00:00+00:00",
  "isActive": true,
  "startLatitude": 25.2532,
  "startLongitude": 55.3657,
  "endLatitude": 25.2048,
  "endLongitude": 55.2708,
  "code": "X8P4Q2ZW",
  "description": "Temporary sharing during airport pickup.",
  "createdOn": "2026-03-23T10:00:00+00:00",
  "updatedOn": "2026-03-23T10:00:00+00:00"
}
```

**Error (400 invalid dates):**

```json
{
  "message": "The request is invalid.",
  "errors": {
    "EndTime": [
      "EndTime must be greater than StartTime."
    ]
  }
}
```

### 4) GET `/api/trips/{tripPublicId}`

**Success (200):**

```json
{
  "publicId": "8b4c2f36-2ee8-49af-a14d-579f8a6d6f4d",
  "name": "Airport Pickup",
  "title": "Terminal 3 meetup",
  "startTime": "2026-03-23T10:00:00+00:00",
  "endTime": "2026-03-23T13:00:00+00:00",
  "isActive": true,
  "startLatitude": 25.2532,
  "startLongitude": 55.3657,
  "endLatitude": 25.2048,
  "endLongitude": 55.2708,
  "code": "X8P4Q2ZW",
  "description": "Temporary sharing during airport pickup.",
  "createdOn": "2026-03-23T10:00:00+00:00",
  "updatedOn": "2026-03-23T10:00:00+00:00"
}
```

**Error (404):**

```json
{
  "type": "https://httpstatuses.com/404",
  "title": "Not Found",
  "status": 404,
  "detail": "Trip not found.",
  "instance": "/api/trips/2f6099d7-c7b8-4ff2-8e4f-6fda7f358170",
  "traceId": "00-2aa75437ec9ec6a8a8818de8dd88574e-24156ab3b39cbaf1-00"
}
```

### 5) POST `/api/trips/{tripPublicId}/end`

**Request body (optional for backward compatibility):**

```json
{
  "endLatitude": 25.2048,
  "endLongitude": 55.2708
}
```

Both `endLatitude` and `endLongitude` are optional. Older clients can continue calling the endpoint without a request body.
If provided, both fields must be provided together and must be valid coordinates:
- `endLatitude`: `-90` to `90`
- `endLongitude`: `-180` to `180`

**Success (200):**

```json
{
  "publicId": "8b4c2f36-2ee8-49af-a14d-579f8a6d6f4d",
  "name": "Airport Pickup",
  "title": "Terminal 3 meetup",
  "startTime": "2026-03-23T10:00:00+00:00",
  "endTime": "2026-03-23T13:00:00+00:00",
  "isActive": false,
  "startLatitude": 25.2532,
  "startLongitude": 55.3657,
  "endLatitude": 25.2048,
  "endLongitude": 55.2708,
  "code": "X8P4Q2ZW",
  "description": "Temporary sharing during airport pickup.",
  "createdOn": "2026-03-23T10:00:00+00:00",
  "updatedOn": "2026-03-23T12:35:00+00:00"
}
```

**Error (404):**

```json
{
  "type": "https://httpstatuses.com/404",
  "title": "Not Found",
  "status": 404,
  "detail": "Trip not found.",
  "instance": "/api/trips/5f7f9d38-1adf-4fb6-8e33-a6208b08c2f7/end",
  "traceId": "00-1c76cf6f3408f6762670a399dfcecf17-a50a7dd07be5e3dd-00"
}
```

### 6) POST `/api/trips/join`

**Success (200):**

```json
{
  "tripMemberPublicId": "3ecb012c-e58a-4b7d-9958-4d2cf6c1eb73",
  "memberPublicId": "7f8fdab1-f786-4f2b-b50b-0dc0e0f5902d",
  "tripPublicId": "8b4c2f36-2ee8-49af-a14d-579f8a6d6f4d",
  "memberName": "Alice Johnson",
  "memberEmail": "alice@example.com",
  "memberDisplayName": "Alice",
  "isActive": true,
  "joinedOn": "2026-03-23T10:05:00+00:00"
}
```

**Error (409 already joined):**

```json
{
  "type": "https://httpstatuses.com/409",
  "title": "Conflict",
  "status": 409,
  "detail": "Member already joined this trip.",
  "instance": "/api/trips/join",
  "traceId": "00-0f5bf2f66a246b90cf2c95b5e2d8a8f3-92456f45ea2e28c6-00"
}
```

### 7) POST `/api/trips/{tripPublicId}/leave`

**Success (200):**

```json
{
  "tripMemberPublicId": "3ecb012c-e58a-4b7d-9958-4d2cf6c1eb73",
  "memberPublicId": "7f8fdab1-f786-4f2b-b50b-0dc0e0f5902d",
  "tripPublicId": "8b4c2f36-2ee8-49af-a14d-579f8a6d6f4d",
  "memberName": "Alice Johnson",
  "memberEmail": "alice@example.com",
  "memberDisplayName": "Alice",
  "isActive": false,
  "joinedOn": "2026-03-23T10:05:00+00:00"
}
```

**Error (403 not active):**

```json
{
  "type": "https://httpstatuses.com/403",
  "title": "Forbidden",
  "status": 403,
  "detail": "Member is not active in this trip.",
  "instance": "/api/trips/8b4c2f36-2ee8-49af-a14d-579f8a6d6f4d/leave",
  "traceId": "00-a7ed5c43943cb8009a8908fe7fd59243-b6d8b1f9f230b579-00"
}
```

### 8) POST `/api/trips/{tripPublicId}/locations`

**Success (200):**

```json
{
  "tripPublicId": "8b4c2f36-2ee8-49af-a14d-579f8a6d6f4d",
  "memberPublicId": "7f8fdab1-f786-4f2b-b50b-0dc0e0f5902d",
  "recordedAt": "2026-03-23T10:06:10+00:00",
  "updatedOn": "2026-03-23T10:06:12+00:00"
}
```

**Error (403 trip ended/outside window):**

```json
{
  "type": "https://httpstatuses.com/403",
  "title": "Forbidden",
  "status": 403,
  "detail": "Trip is not active or outside its valid time window.",
  "instance": "/api/trips/8b4c2f36-2ee8-49af-a14d-579f8a6d6f4d/locations",
  "traceId": "00-f1942eb2f906f55c72e9e066dd7078f8-55cf6479b43dc959-00"
}
```

### 9) GET `/api/trips/{tripPublicId}/locations/latest`

**Success (200):**

```json
[
  {
    "memberPublicId": "7f8fdab1-f786-4f2b-b50b-0dc0e0f5902d",
    "tripPublicId": "8b4c2f36-2ee8-49af-a14d-579f8a6d6f4d",
    "latitude": 25.2048,
    "longitude": 55.2708,
    "accuracy": 7.5,
    "speed": 3.2,
    "heading": 154.0,
    "recordedAt": "2026-03-23T10:06:10+00:00",
    "updatedOn": "2026-03-23T10:06:12+00:00"
  }
]
```

**Error (404):**

```json
{
  "type": "https://httpstatuses.com/404",
  "title": "Not Found",
  "status": 404,
  "detail": "Trip not found.",
  "instance": "/api/trips/2f6099d7-c7b8-4ff2-8e4f-6fda7f358170/locations/latest",
  "traceId": "00-68b6903b15d57f9d597acdb7d923f142-a933f4ea582f2fd0-00"
}
```

### 10) GET `/api/trips/{tripPublicId}/members`

**Success (200):**

```json
[
  {
    "tripMemberPublicId": "3ecb012c-e58a-4b7d-9958-4d2cf6c1eb73",
    "memberPublicId": "7f8fdab1-f786-4f2b-b50b-0dc0e0f5902d",
    "tripPublicId": "8b4c2f36-2ee8-49af-a14d-579f8a6d6f4d",
    "memberName": "Alice Johnson",
    "memberEmail": "alice@example.com",
    "memberDisplayName": "Alice",
    "isActive": true,
    "joinedOn": "2026-03-23T10:05:00+00:00"
  }
]
```

**Error (403):**

```json
{
  "type": "https://httpstatuses.com/403",
  "title": "Forbidden",
  "status": 403,
  "detail": "Trip is not active or outside its valid time window.",
  "instance": "/api/trips/8b4c2f36-2ee8-49af-a14d-579f8a6d6f4d/members",
  "traceId": "00-fba3e7f472ba9e2cf7eff661134ed2d1-05e98060705bb012-00"
}
```
