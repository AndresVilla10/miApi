using Microsoft.AspNetCore.Diagnostics;

var builder = WebApplication.CreateBuilder(args);

// Render inyecta el puerto por la variable de entorno PORT.
// Sin esto, la app no encontraria el puerto que el servicio le asigno.
var puerto = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(puerto))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{puerto}");
}

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Manejador global de excepciones no controladas.
// Allows que cualquier excepcion sin capturar se convierta en un 500
// con la respuesta estandar de error del servidor (ProblemDetails).
app.UseExceptionHandler(errorApp => errorApp.Run(async context =>
{
    var error = context.Features.Get<IExceptionHandlerFeature>()?.Error;
    app.Logger.LogError(error, "Excepcion no controlada: {Mensaje}", error?.Message);

    await Results.Problem(
        statusCode: StatusCodes.Status500InternalServerError,
        title: "Error interno del servidor",
        detail: "Ocurrio un error inesperado al procesar la solicitud."
    ).ExecuteAsync(context);
}));

app.UseSwagger();
app.UseSwaggerUI();

var estudiantes = new List<Estudiante>
{
    // new() { Id = 1, Nombre = "Lopez", Edad = 21, Carrera = "Sistemas" },
    new() { Id = 2, Nombre = "Maria", Edad = 22, Carrera = "Medicina" },
    new() { Id = 3, Nombre = "Carlos", Edad = 23, Carrera = "Derecho" }
};

app.MapGet("/", () => "lopez te robaron!");

app.MapGet("/estudiantes", () => Results.Ok(estudiantes));

app.MapGet("/estudiantes/{id}", (int id) =>
{
    var estudiante = estudiantes.FirstOrDefault(e => e.Id == id);
    return estudiante is null
        ? Results.NotFound($"No existe el estudiante con id {id}")
        : Results.Ok(estudiante);
});

app.MapPost("/estudiantes", (Estudiante nuevo) =>
{
    if (string.IsNullOrWhiteSpace(nuevo.Nombre) || nuevo.Edad <= 0)
    {
        return Results.BadRequest("Nombre y Edad son obligatorios (edad mayor a 0)");
    }

    nuevo.Id = estudiantes.Count > 0 ? estudiantes.Max(e => e.Id) + 1 : 1;
    estudiantes.Add(nuevo);
    return Results.Created($"/estudiantes/{nuevo.Id}", nuevo);
});

app.MapPut("/estudiantes/{id}", (int id, Estudiante actualizado) =>
{
    var estudiante = estudiantes.FirstOrDefault(e => e.Id == id);
    if (estudiante is null)
    {
        return Results.NotFound($"No existe el estudiante con id {id}");
    }

    estudiante.Nombre = actualizado.Nombre;
    estudiante.Edad = actualizado.Edad;
    estudiante.Carrera = actualizado.Carrera;
    return Results.Ok(estudiante);
});

app.MapDelete("/estudiantes/{id}", (int id) =>
{
    var estudiante = estudiantes.FirstOrDefault(e => e.Id == id);
    if (estudiante is null)
    {
        return Results.NotFound($"No existe el estudiante con id {id}");
    }

    estudiantes.Remove(estudiante);
    // RETO 3 - 204 No Content: operacion exitosa pero cuerpo vacio.
    // Antes devolvia Results.Ok con mensaje, ahora devuelve 204 como pide el reto.
    return Results.NoContent();
});

// ============================================================
// LISTA DE RETOS DE CODIGOS DE ESTADO HTTP
// Base para pruebas: /retos/xxx
// ============================================================

// RETO 1 - 200 OK: GET que devuelve listado en JSON
app.MapGet("/retos/200", () => Results.Ok(estudiantes));

// RETO 2 - 201 Created: POST que simula guardado y devuelve ruta + ID
app.MapPost("/retos/201", (Estudiante nuevo) =>
{
    nuevo.Id = estudiantes.Count > 0 ? estudiantes.Max(e => e.Id) + 1 : 1;
    estudiantes.Add(nuevo);
    return Results.Created($"/estudiantes/{nuevo.Id}", nuevo);
});

// RETO 3 - 204 No Content: DELETE exitoso con cuerpo vacio
app.MapDelete("/retos/204/{id}", (int id) =>
{
    var estudiante = estudiantes.FirstOrDefault(e => e.Id == id);
    if (estudiante is null)
    {
        return Results.NotFound($"No existe el estudiante con id {id}");
    }

    estudiantes.Remove(estudiante);
    return Results.NoContent(); // <- 204: sin cuerpo
});

// RETO 4 - 400 Bad Request: valida campo obligatorio vacio o nulo
app.MapPost("/retos/400", (Estudiante nuevo) =>
{
    if (string.IsNullOrWhiteSpace(nuevo.Nombre))
    {
        return Results.BadRequest(new { error = "El campo 'Nombre' es obligatorio y no puede estar vacio ni nulo." });
    }
    if (nuevo.Edad <= 0)
    {
        return Results.BadRequest(new { error = "El campo 'Edad' debe ser mayor a 0." });
    }

    return Results.Ok(new { mensaje = "Datos validos", datos = nuevo });
});

// RETO 5 - 401 Unauthorized: exige encabezado Authorization
app.MapGet("/retos/401", (HttpRequest request) =>
{
    if (!request.Headers.ContainsKey("Authorization"))
    {
        return Results.Unauthorized();
    }

    return Results.Ok(new { mensaje = "Autorizado correctamente" });
});

// RETO 6 - 403 Forbidden: autenticado pero sin permiso (rol Estudiante vs Docente)
app.MapDelete("/retos/403/{id}", (int id, HttpRequest request) =>
{
    // Se simula control de roles con el encabezado X-Rol.
    // Ejemplo: X-Rol: Estudiante -> denegado. X-Rol: Docente -> permitido.
    var rol = request.Headers["X-Rol"].ToString();

    if (string.IsNullOrWhiteSpace(rol))
    {
        // Sin rol no podemos autorizar la accion -> 401 para diferenciar de 403
        return Results.Unauthorized();
    }

    if (!string.Equals(rol, "Docente", StringComparison.OrdinalIgnoreCase))
    {
        return Results.Json(
            new { error = $"Acceso denegado. El rol '{rol}' no tiene permiso. Se requiere rol 'Docente'." },
            statusCode: StatusCodes.Status403Forbidden);
    }

    return Results.Ok(new { mensaje = $"Accion permitida para Docente sobre el recurso {id}" });
});

// RETO 7 - 404 Not Found: GET por ID que no existe
app.MapGet("/retos/404/{id}", (int id) =>
{
    var estudiante = estudiantes.FirstOrDefault(e => e.Id == id);
    return estudiante is null
        ? Results.NotFound(new { error = $"No existe el estudiante con id {id}" })
        : Results.Ok(estudiante);
});

// RETO 8 - 409 Conflict: registro duplicado (nombre o codigo unico ya existe)
app.MapPost("/retos/409", (Estudiante nuevo) =>
{
    // Validacion previa: sin nombre no se puede evaluar el conflicto,
    // y evita un NullReferenceException al comparar.
    if (string.IsNullOrWhiteSpace(nuevo.Nombre))
    {
        return Results.BadRequest(new { error = "El campo 'Nombre' es obligatorio y no puede estar vacio ni nulo." });
    }

    var existe = estudiantes.Any(e =>
        string.Equals(e.Nombre?.Trim(), nuevo.Nombre.Trim(), StringComparison.OrdinalIgnoreCase));

    if (existe)
    {
        return Results.Conflict(new { error = $"Ya existe un estudiante con el nombre '{nuevo.Nombre}'." });
    }

    nuevo.Id = estudiantes.Count > 0 ? estudiantes.Max(e => e.Id) + 1 : 1;
    estudiantes.Add(nuevo);
    return Results.Created($"/estudiantes/{nuevo.Id}", nuevo);
});

// RETO 9 - 422 Unprocessable Entity: JSON bien formado pero viola regla de negocio
app.MapPost("/retos/422", (NotaEstudiante dato) =>
{
    // Regla 1: nota entre 0.0 y 5.0
    if (dato.Nota < 0.0 || dato.Nota > 5.0)
    {
        return Results.UnprocessableEntity(new { error = "La nota debe estar entre 0.0 y 5.0." });
    }

    // Regla 2: fecha de nacimiento no puede ser futura
    if (dato.FechaNacimiento.Date > DateTime.Today)
    {
        return Results.UnprocessableEntity(new { error = "La fecha de nacimiento no puede estar en el futuro." });
    }

    return Results.Ok(new { mensaje = "Datos procesados correctamente", datos = dato });
});

// RETO 10 - 500 Internal Server Error: fallo interno no controlado
app.MapGet("/retos/500", () =>
{
    // Simulamos un fallo interno (ej. division por cero).
    // La excepcion NO se captura aqui: el manejador global registrado arriba
    // la convierte en un 500 con la respuesta estandar de error (ProblemDetails).
    int cero = 0;
    int resultado = 10 / cero;
    return Results.Ok(resultado);
});

app.Run();

class Estudiante
{
    public int Id { get; set; }
    public string Nombre { get; set; } = "";
    public int Edad { get; set; }
    public string Carrera { get; set; } = "";
}

// Modelo solo para el RETO 9 (422)
class NotaEstudiante
{
    public double Nota { get; set; }
    public DateTime FechaNacimiento { get; set; }
}