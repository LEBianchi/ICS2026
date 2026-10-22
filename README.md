# Trabajo Práctico Integrador
## Desarrollo de Software
### Backend

## Introducción
Se desea desarrollar una plataforma de comercio electrónico (E-commerce). 
En esta primera etapa el objetivo es construir el módulo de Órdenes, permitiendo la gestión completa de éstas.

## Visión General del Producto
Del relevamiento preliminar se identificaron los siguientes requisitos:
- Los visitantes pueden consultar los productos sin necesidad de estar registrados o iniciar sesión.
- Para realizar un pedido se requiere el inicio de sesión.
- Una orden, para ser aceptada, debe incluir la información básica del cliente, envío y facturación.
- Antes de registrar la orden se debe verificar la disponibilidad de stock (o existencias) de los productos.
- Si la orden es exitosa hay que actualizar el stock de cada producto.
- Se deben poder consultar órdenes individuales o listar varias con posibilidad de filtrado.
- Será necesario el cambio de estado de una orden a medida que avanza en su ciclo de vida.
- Los administradores solo pueden gestionar los productos (alta, modificación y baja) y actualizar el estado de la orden.
- Los clientes pueden crear y consultar órdenes.

[Documento completo](https://frtutneduar.sharepoint.com/:b:/s/DSW2025/ETueAd4rTe1Gilj_Yfi64RYB5oz9s2dOamxKSfMFPREbiA?e=azZcwg) 

## Alcance para el Primer Parcial
> [!IMPORTANT]
> Del apartado `IMPLEMENTACIÓN` (Pag. 7), completo hasta el punto `6` (inclusive)


### Características de la Solución

- Lenguaje: C# 12.0
- Plataforma: .NET 8


## Configuración local

La cadena de conexión y la clave JWT **no están en el repositorio**. Cada integrante las carga en su máquina con User Secrets (se guardan fuera del repo y solo se leen en modo Development).

Desde la raíz del repositorio:

1. Generar una clave propia (en Git Bash):

```bash
   openssl rand -base64 64 | tr -d '\n'; echo
```

2. Cargar los dos secretos:

```bash
   dotnet user-secrets set "ConnectionStrings:Dsw2025TpiEntities" 'Data Source=(localdb)\MSSQLLocalDB;Initial Catalog=Dsw2025Tpi;Integrated Security=True;' --project Dsw2025Tpi.Api
   dotnet user-secrets set "Jwt:Key" 'PEGAR_LA_CLAVE_GENERADA' --project Dsw2025Tpi.Api
```

3. Verificar:

```bash
   dotnet user-secrets list --project Dsw2025Tpi.Api
```

Si falta alguno de los dos valores, la API no arranca y muestra un mensaje indicando cuál falta.

En Docker y en Azure se usan variables de entorno, con doble guion bajo en lugar de los dos puntos:

| Variable | Contenido |
| --- | --- |
| `ConnectionStrings__Dsw2025TpiEntities` | Cadena de conexión a la base |
| `Jwt__Key` | Clave de firma de los tokens (mínimo 32 caracteres) |
| `SeedAdmin__Username` | Usuario del administrador inicial |
| `SeedAdmin__Email` | Email del administrador inicial |
| `SeedAdmin__Password` | Contraseña del administrador inicial |

### Administrador inicial

La API crea el administrador al arrancar a partir de la configuración. En desarrollo:

```bash
dotnet user-secrets set "SeedAdmin:Username" "admin" --project Dsw2025Tpi.Api
dotnet user-secrets set "SeedAdmin:Email" "admin@dsw2025.com" --project Dsw2025Tpi.Api
dotnet user-secrets set "SeedAdmin:Password" "CONTRASEÑA_FUERTE" --project Dsw2025Tpi.Api
```

La contraseña debe tener al menos 8 caracteres, con mayúscula, minúscula, número y símbolo.

## Ejecutar con Docker

La imagen se construye con un Dockerfile multi-stage: una etapa compila con el SDK de .NET 8 y la imagen final solo lleva el runtime de ASP.NET y los binarios publicados.

Desde la raíz del repositorio:

1. Red y SQL Server en contenedor:

```bash
docker network create tp3-red
docker run -d --name sql-tp3 --network tp3-red -e "ACCEPT_EULA=Y" -e "MSSQL_SA_PASSWORD=Tp3.Local-2026" -p 14333:1433 mcr.microsoft.com/mssql/server:2022-latest
```

2. Construir la imagen:

```bash
docker build -t dsw2025tpi-api:local .
```

3. Ejecutar la API (reemplazar la clave y la contraseña del admin):

```bash
docker run -d --name api-tp3 --network tp3-red -p 8080:8080 \
  -e "ConnectionStrings__Dsw2025TpiEntities=Server=sql-tp3,1433;Database=Dsw2025Tpi;User Id=sa;Password=Tp3.Local-2026;TrustServerCertificate=True;" \
  -e "Jwt__Key=CLAVE_DE_AL_MENOS_32_CARACTERES" \
  -e "SeedAdmin__Username=admin" \
  -e "SeedAdmin__Email=admin@dsw2025.com" \
  -e "SeedAdmin__Password=CONTRASEÑA_FUERTE" \
  dsw2025tpi-api:local
```

4. Verificar: `docker logs api-tp3`, `http://localhost:8080/healthcheck` y `http://localhost:8080/swagger`.

Para limpiar: `docker rm -f api-tp3 sql-tp3` y `docker network rm tp3-red`.
