# Liquidación de Sueldos — .NET 10 · Razor Pages · PostgreSQL

Versión web multi-estudio del prototipo HTML «Liquidación de Sueldos» (misma estética y misma lógica de cálculo).

**Stack:** .NET 10 · ASP.NET Core Razor Pages · EF Core 10 + Npgsql · PostgreSQL · ASP.NET Core Identity.

## Qué incluye

- **Multi-estudio (SaaS):** cada estudio contable se registra solo y ve únicamente sus datos (filtro global por estudio en EF Core).
- **Roles:** Administrador (todo) y Operador (empleados, liquidación, recibos, vacaciones y notas).
- **Empresas** con logo y contribuciones patronales · **Convenios** con categorías, parámetros y **paritaria** · **Conceptos** adicionales.
- **Empleados:** el sueldo básico sale de la categoría (se puede crear la categoría al dar de alta al empleado).
- **Liquidación** por período/tipo, con **cierre** (los recibos quedan congelados) y **rectificativas** versionadas.
- **Recibo** imprimible en A4 horizontal (original + duplicado) con gráfico de costo laboral.
- **Vacaciones** y **notas** de vacaciones/suspensión imprimibles · **Auditoría** de todas las acciones.

## Probarlo en tu PC

Necesitás: [.NET 10 SDK](https://dotnet.microsoft.com/download) y [Docker Desktop](https://www.docker.com/products/docker-desktop/) (solo para la base de datos).

```bash
docker compose up -d                       # levanta PostgreSQL local
dotnet build                               # compila todo
dotnet test                                # corre las pruebas del motor de cálculo
dotnet run --project src/LiquidacionSueldos.Web
```

Abrí la dirección que muestra la consola (por ejemplo `http://localhost:5000`), tocá **Registrar mi estudio** y listo.

> Este código se escribió sin poder compilarlo. Si `dotnet build` o `dotnet test` muestran algún error, copialo y pasámelo: se corrige enseguida.

### Migraciones (recomendado antes de publicar)

Sin migraciones, la app crea las tablas sola (`EnsureCreated`). Para un programa definitivo conviene usar migraciones, así los cambios futuros se aplican sin perder datos:

```bash
dotnet tool install --global dotnet-ef
dotnet ef migrations add Inicial --project src/LiquidacionSueldos.Web
```

Subí la carpeta `Migrations` a GitHub. Al arrancar, la app detecta las migraciones y las aplica automáticamente.
Si ya habías corrido la app sin migraciones, borrá la base local antes: `docker compose down -v`.

## Publicar en Railway

1. Subí esta carpeta a un repositorio de GitHub.
2. En [railway.app](https://railway.app): **New Project → Deploy from GitHub repo** y elegí el repositorio (Railway detecta el `Dockerfile`).
3. En el mismo proyecto: **+ New → Database → Add PostgreSQL**.
4. Entrá al servicio de la app → **Variables → New Variable**:
   - Nombre: `DATABASE_URL`
   - Valor: `${{Postgres.DATABASE_URL}}` (si tu base se llama distinto, elegila desde el selector de referencias).
5. Servicio de la app → **Settings → Networking → Generate Domain**.
6. Abrí el dominio, registrá tu estudio y empezá a cargar empresas.

La app espera a que la base esté lista, crea las tablas y guarda las claves de sesión en la base (los usuarios no se desconectan al redeployar).

## Variables de entorno

| Variable | Para qué |
|---|---|
| `DATABASE_URL` | Conexión a PostgreSQL (`postgres://usuario:clave@host:puerto/base`). Railway la provee. |
| `ConnectionStrings__Default` | Alternativa en formato Npgsql si no hay `DATABASE_URL`. |
| `PORT` | Puerto HTTP (lo define Railway). |

## Estructura

```
src/LiquidacionSueldos.Web
  Domain/     entidades y motor de cálculo (función pura, sin base de datos)
  Data/       DbContext, datos iniciales
  Services/   liquidación (borrador, cierre, rectificativa), auditoría, contexto del estudio
  Pages/      pantallas Razor Pages
tests/LiquidacionSueldos.Tests   pruebas del cálculo (xUnit)
```

## Decisiones de diseño

- **Cada concepto se redondea a 2 decimales**, igual que en un recibo real: los totales coinciden con la suma de los renglones impresos.
- **Un recibo cerrado nunca se recalcula:** al cerrar se guarda una foto (JSON) con valores, parámetros del convenio y datos de empresa/empleado. Para corregirlo se crea una rectificativa (versión siguiente).
- **Porcentajes:** se cargan en % en pantalla y se guardan como fracción (11 % → 0,11).
- Fuera de alcance de esta versión: exportación a ARCA (Libro de Sueldos Digital).
