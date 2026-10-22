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
