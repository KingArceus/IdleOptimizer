using IdleOptimizer.Api.Models;
using IdleOptimizer.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace IdleOptimizer.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SyncController : ControllerBase
{
    private readonly IMongoService _mongoService;
    private readonly ILogger<SyncController> _logger;

    public SyncController(IMongoService mongoService, ILogger<SyncController> logger)
    {
        _mongoService = mongoService;
        _logger = logger;
    }

    [HttpPost("save")]
    public async Task<IActionResult> Save([FromBody] SyncData syncData)
    {
        try
        {
            if (syncData == null)
            {
                return BadRequest(new { error = "SyncData cannot be null" });
            }

            if (string.IsNullOrWhiteSpace(syncData.UserId))
            {
                return BadRequest(new { error = "UserId is required" });
            }

            await _mongoService.SaveSyncDataAsync(syncData);
            return Ok(new { message = "Data saved successfully", userId = syncData.UserId });
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid argument in save request");
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving sync data for userId: {UserId}", syncData?.UserId);
            return StatusCode(500, new { error = "An error occurred while saving data" });
        }
    }

    [HttpGet("load")]
    public async Task<IActionResult> Load([FromQuery] string userId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return BadRequest(new { error = "UserId is required" });
            }

            var syncData = await _mongoService.LoadSyncDataAsync(userId);
            
            if (syncData == null)
            {
                return NotFound(new { error = $"No data found for userId: {userId}" });
            }

            return Ok(syncData);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid argument in load request");
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading sync data for userId: {UserId}", userId);
            return StatusCode(500, new { error = "An error occurred while loading data" });
        }
    }
}

