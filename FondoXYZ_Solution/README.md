# Fondo de Bienestar XYZ - Sistema de Reservas en Línea

Este proyecto es la solución a la **Prueba Técnica para Analista Desarrollador .NET**, enfocada en la gestión de reservas para sedes recreativas y apartamentos del Fondo XYZ. 

## Tecnologías y Arquitectura Utilizadas

*   **Framework:** .NET 10 (ASP.NET Core MVC).
*   **Lenguaje:** C# 13.
*   **Base de Datos:** Microsoft SQL Server.
*   **ORM:** Entity Framework Core (Code-First) combinado con ADO.NET para la ejecución estricta de Procedimientos Almacenados (SP).
*   **Seguridad:** ASP.NET Core Identity (Autenticación basada en Cookies).
*   **Front-End:** Bootstrap 5, HTML5, CSS3, Vanilla JavaScript.

## Requerimientos Cumplidos según el Documento Técnico

1.  **Estructura BD Relacional (SQL Server):** Se utilizó Entity Framework Core con enfoque Code-First, garantizando la integridad referencial entre Usuarios, Sedes, Alojamientos, Tarifas y Reservas.
2.  **Procedimientos Almacenados (SPs):**
    *   `sp_ConsultarDisponibilidadPorFechas`: Permite encontrar alojamientos libres en un rango.
    *   `sp_ConsultarDisponibilidadPorFechasYPersonas`: Encuentra disponibilidad cruzando fechas y capacidad máxima.
    *   `sp_ConsultarTarifaAlojamiento`: Devuelve las tarifas vigentes considerando sede, tipo y acomodación.
    *   `sp_CalcularTarifaReserva`: Realiza el cálculo matemático (noches * tarifa base + recargos por personas adicionales) según las reglas de negocio (ej. $16.000 persona extra).
3.  **Seguridad y Autenticación (.NET Identity):**
    *   Formulario funcional de **Registro** de usuarios.
    *   Formulario de **Login** (el controlador `HomeController` exige `[Authorize]`).
    *   Recuperación de contraseña vía **SMTP** (Configurado para envío real a través de Gmail).
4.  **Desarrollo de Formularios y CRUD:**
    *   Catálogo interactivo de Sedes y Destinos.
    *   Detalle de unidades habitacionales (Apartamentos y Cabañas).
    *   Buscador dinámico de Disponibilidad (conectado vía AJAX a los SPs de BD).
    *   CRUD completo de **Reservas** (Crear, Listar "Mis Reservas", Editar, Cancelar con recalculo de costos).

## Pasos para Ejecutar el Proyecto

### 1. Cadena de Conexión (Connection String)
La aplicación utiliza autenticación de Windows por defecto (`Integrated Security=True`). La cadena de conexión está ubicada en el archivo `FondoXYZ.Web/appsettings.json`:

```json
"ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=FondoXYZ_DB;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True;"
}
```
*Si tu servidor SQL tiene un nombre de instancia (ej. `localhost\SQLEXPRESS`), debes actualizar el valor de `Server`.*

### 2. Configuración de Base de Datos y SPs
No necesitas adjuntar un `.bak`, el proyecto está configurado para generar la base de datos automáticamente con datos semilla:
1. Abre la **Consola del Administrador de Paquetes** (Package Manager Console) en Visual Studio.
2. Selecciona el proyecto `FondoXYZ.Infrastructure` como proyecto predeterminado.
3. Ejecuta el comando:
   ```powershell
   Update-Database
   ```
   *Esto creará la base de datos `FondoXYZ_DB`, todas las tablas, los datos iniciales (sedes, alojamientos, tarifas) y creará automáticamente los 4 Procedimientos Almacenados exigidos en la prueba (gracias a la migración `20260520_StoredProcedures`).*

### 3. Configuración de Correo Electrónico (SMTP)
Para la recuperación de contraseña, el archivo `appsettings.json` tiene pre-configurado un servidor SMTP de pruebas:
```json
"SmtpSettings": {
    "Host": "smtp.gmail.com",
    "Port": 587,
    "EnableSsl": true,
    "UserName": "tu_correo@gmail.com",
    "Password": "tu_app_password"
}
```

### 4. Ejecución de Pruebas Unitarias (Opcional)
Se incluye un proyecto `FondoXYZ.Tests` con **79 pruebas unitarias** usando xUnit y Moq, validando las reglas de liquidación, los recargos de personas adicionales, el cálculo de lavandería (solo en apartamentos) y las restricciones de capacidad. Puedes ejecutarlas desde el Explorador de Pruebas de Visual Studio.

---
**Desarrollado para:** Prueba Técnica - Analista Desarrollador .NET
