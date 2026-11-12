using System.Collections.Concurrent;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Funkos.RealTime.Notificaciones;

[Authorize]
public class NotificacionesHub  : Hub
{
    
    private static readonly ConcurrentDictionary<string, int> messageCounts = new();
     // ==========================================
    // MÉTODOS DEL CLIENTE
    // ==========================================
    
    /// <summary>
    /// El cliente llama a este metodo para enviar un mensaje a todos los conectados.
    /// </summary>
    public async Task SendMessage(string usuario, string mensaje)
    {
        // Se obtiene el ID del usuario autenticado leyendo los Claims del contexto de la conexión.
        // Como se asume que el usuario está autenticado se pone --> !
        var userId = Context.User!.FindFirstValue(ClaimTypes.NameIdentifier)!;

        // Incrementa en 1 el contador de mensajes del usuario en el dicc. 
        // Si el usuario ya existe --> toma el valor actual (c) y le suma 1.
        // Si no existe --> lo inicializa en 1.
        var count = messageCounts.AddOrUpdate(userId, 1, (_, c) => c + 1);

        // Comprueba si el usuario ha superado el límite permitido de mensajes enviados
        if (count > 100)  // Límite == 100 mensajes por conexión
        {
            // Se envía un mensaje de error al cliente ecpecifico
            await Clients.Caller.SendAsync("Error", "Limite de mensajes alcanzado");
            return;
        }

        // Si NO ha superado el límite, reenvía el mensaje recibido + el Id del usuario a TODOS los clientes conectados
        await Clients.All.SendAsync("ReceiveMessage", usuario, mensaje, DateTime.UtcNow);
    }
    
    /// <summary>
    /// Enviar un mensaje solo al cliente que lo llamo.
    /// </summary>
    public async Task SendToCaller(string mensaje)
    {
        await Clients.Caller.SendAsync("ReceiveMessage", "Sistema", mensaje, DateTime.UtcNow);
    }
    
    /// <summary>
    /// Enviar un mensaje a un grupo especifico.
    /// </summary>
    public async Task SendToGroup(string groupName, string mensaje)
    {
        await Clients.Group(groupName).SendAsync("ReceiveMessage", "Sistema", mensaje, DateTime.UtcNow);
    }
    
    // ==========================================
    //  MÉTODOS DE CICLO DE VIDA 
    // ==========================================
    /// <summary>
    /// Se ejecuta cuando un cliente se conecta.
    /// Asignamos grupos automaticamente.
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        var userId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier) 
                     ?? Context.User?.FindFirstValue("sub") 
                     ?? Context.User?.FindFirstValue("id");

        // Buscamos el email intentando varias opciones comunes
        var email = Context.User?.FindFirstValue(ClaimTypes.Email) 
                    ?? Context.User?.FindFirstValue(ClaimTypes.Name) 
                    ?? Context.User?.FindFirstValue("email");
        
        var isAdmin = Context.User?.IsInRole("ADMIN") ?? false;

        if (userId is not null)
        {
            // Grupo privado: solo este usuario recibe sus notificaciones
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user-{userId}");
        }
        // Notificar a todos que se conecto un admin y un usuario
        if (isAdmin)
        {
            // Grupo de admins: todas las notificaciones de admin
            await Groups.AddToGroupAsync(Context.ConnectionId, "admins");
            await Clients.Group("admins").SendAsync("AdminConectado", new
            {
                UsuarioId = userId ?? "Desconocido",
                Email = email ?? "Sin email",
                Mensaje = $"El administrador {email ?? "Desconocido"} se ha conectado.",
                FechaLogin = DateTime.UtcNow
            });
        }
        else
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, "users");
            await Clients.Group("users").SendAsync("UsuarioConectado", new
            {
                UsuarioId = userId ?? "Desconocido",
                Email = email ?? "Sin email",
                Mensaje = $"El Usuario {email ?? "Desconocido"} se ha conectado.",
                FechaLogin = DateTime.UtcNow
            });
        }
        await base.OnConnectedAsync();
    }
    
    /// <summary>
    /// Se ejecuta cuando un cliente se desconecta.
    /// Limpiamos recursos si es necesario.
    /// </summary>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        // Obligatorio: sin limpiar, el ConcurrentDictionary crece sin control
        // Se intenta obtener el Id del usuario que se está desconectando a partir de sus claims.
        // Si ID != nulo && usuario = autenticado --> se limpia su registro.
        if (Context.User?.FindFirstValue(ClaimTypes.NameIdentifier) is string userId)
        {
            // Elimina al usuario del diccionario de contadores para liberar memoria RAM y evitar fugas de memoria (memory leaks).
            messageCounts.TryRemove(userId, out _);
        }

        // SignalR ya elimina la conexion de los grupos automaticamente
        await base.OnDisconnectedAsync(exception);
    }
}