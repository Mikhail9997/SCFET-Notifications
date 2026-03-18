using System.Text.Json.Serialization;
using Application.Common.Interfaces;
using Application.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

namespace WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Administrator")]
public class SystemNotificationController:ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly IKafkaProducer _producer;

    public SystemNotificationController(IConfiguration configuration, IKafkaProducer producer)
    {
        _configuration = configuration;
        _producer = producer;
    }

    [HttpPost("maintenance")]
    public async Task<IActionResult> NotifyMaintenance([FromBody] MaintenanceNotificationDto schedule)
    {
        try
        {
            string message = JsonConvert.SerializeObject(schedule);
            string topic = _configuration["Kafka:Topics:SystemNotifications"] ?? "system-notifications";
            await _producer.ProduceAsync(topic, message);

            return Ok(new {data=message, message="уведомление отправлено", success=true });
        }
        catch(Exception ex)
        {
            return BadRequest(new {message="не удалось отправить уведомление", success=false});
        }
    }
}