using Application.Common.Interfaces;
using Application.DTOs;
using Application.Services;
using AutoMapper;
using Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class FavoritesController: ControllerBase
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IFavoritesService _favoritesService;
    private readonly IMapper _mapper;

    public FavoritesController(ICurrentUserService currentUserService, 
        IFavoritesService favoritesService, 
        IMapper mapper)
    {
        _currentUserService = currentUserService;
        _favoritesService = favoritesService;
        _mapper = mapper;
    }

    [HttpGet("my")]
    public async Task<IActionResult> GetMyFavorites([FromQuery] FilterDto query)
    {
        if (!_currentUserService.UserId.HasValue)
        {
            return Unauthorized();
        }

        Guid userId = _currentUserService.UserId.Value;
        try
        {
            FilterEntity filterEntity = _mapper.Map<FilterEntity>(query);
            var result = await _favoritesService.GetMyAsync(userId, filterEntity);
            
            return Ok(new { message = "Успех", data=result, success=true});
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message, success=false });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Внутренняя ошибка сервера", success=false });
        }
    }

    [HttpPost("add")]
    public async Task<IActionResult> Add([FromBody] AddFavoriteDto dto)
    {
        if (!_currentUserService.UserId.HasValue)
        {
            return Unauthorized();
        }
        try
        {
            Guid userId = _currentUserService.UserId.Value;
            await _favoritesService.AddAsync(userId, dto);
            
            return Ok(new { message = "Успех", success=true});
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message, success=false });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Внутренняя ошибка сервера", success=false });
        }
    }

    [HttpDelete("{notificationId}/remove")]
    public async Task<IActionResult> Remove(Guid notificationId)
    {
        if (!_currentUserService.UserId.HasValue)
        {
            return Unauthorized();
        }

        Guid userId = _currentUserService.UserId.Value;
        try
        {
            await _favoritesService.RemoveAsync(userId, notificationId);
            
            return Ok(new { message = "Успех", success=true});
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message, success=false });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Внутренняя ошибка сервера", success=false });
        }
    }
}