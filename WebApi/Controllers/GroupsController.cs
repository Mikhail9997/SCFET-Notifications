using Application.DTOs;
using Application.Services;
using AutoMapper;
using Core.Interfaces;
using Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

namespace WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class GroupsController: ControllerBase
{
    private readonly IGroupRepository _groupRepository;
    private readonly IUserRepository _userRepository;
    private readonly RedisService _redis;
    private readonly IConfiguration _configuration;
    private readonly IMapper _mapper;

    public GroupsController(IGroupRepository groupRepository, IUserRepository userRepository, RedisService redis, IConfiguration configuration, IMapper mapper)
    {
        _groupRepository = groupRepository;
        _userRepository = userRepository;
        _redis = redis;
        _configuration = configuration;
        _mapper = mapper;
    }
    
    [HttpGet]
    public async Task<IActionResult> GetAllGroups([FromQuery] GroupFilterDto query)
    {
        string cacheKey = $"groups_{JsonConvert.SerializeObject(query)}";
        var minutes = int.Parse(_configuration["Redis:Group:Minutes"] ?? "1");
        int hours = int.Parse(_configuration["Redis:Group:Hours"] ?? "1");
        
        // Всегда проверяем кэш для конкретного запроса (с фильтром или без)
        var cachedDtos = await _redis.GetAsync<List<GroupDto>>(cacheKey);
        if (cachedDtos != null)
        {
            return Ok(cachedDtos.Select(g => new
            {
                g.Id,
                g.Name,
                g.StudentCount
            }));
        }
        // Получаем группы (с фильтром или без)
        List<Group> groups;
        
        if (!string.IsNullOrEmpty(query.Name))
        {
            // С фильтром - напрямую из БД
            groups = await _groupRepository.FilterAsync(query.Name);
        }
        else
        {
            // Без фильтра - пробуем из общего кэша
            var allGroupsCacheKey = "groups";
            var cachedAllDtos = await _redis.GetAsync<List<GroupDto>>(allGroupsCacheKey);
            
            if (cachedAllDtos != null)
            {
                // Если есть в кэше - используем и сохраняем для текущего ключа
                await _redis.SetAsync(cacheKey, cachedAllDtos, TimeSpan.FromMinutes(minutes));
                return Ok(cachedAllDtos.Select(g => new
                {
                    g.Id,
                    g.Name,
                    g.StudentCount
                }));
            }
            
            // Нет в кэше - получаем из БД
            groups = (List<Group>)await _groupRepository.GetAllAsync();
            
            // Сохраняем все группы в кэш
            var allDtos = _mapper.Map<List<GroupDto>>(groups);
            await _redis.SetAsync(allGroupsCacheKey, allDtos, TimeSpan.FromMinutes(hours));
        }
        
        // Маппим в DTO
        var resultDtos = _mapper.Map<List<GroupDto>>(groups);
        
        // Сохраняем в кэш для этого конкретного запроса
        await _redis.SetAsync(cacheKey, resultDtos, TimeSpan.FromMinutes(minutes));
        
        return Ok(resultDtos.Select(g => new
        {
            g.Id,
            g.Name,
            g.StudentCount
        }));
    }
    
    [HttpGet("{id}")]
    public async Task<IActionResult> GetGroupById(Guid id)
    {
        var group = await _groupRepository.GetByIdAsync(id);
        if (group == null)
            return NotFound(new { message = "Группа не найдена" });

        return Ok(new
        {
            group.Id,
            group.Name,
            Students = group.Students.Select(s => new
            {
                s.Id,
                s.FirstName,
                s.LastName,
                s.Email
            })
        });
    }
    
    [HttpPost]
    [Authorize(Roles = "Administrator")]
    public async Task<IActionResult> CreateGroup([FromBody] CreateGroupDto createDto)
    {
        var existingGroup = await _groupRepository.GetByNameAsync(createDto.Name);
        if (existingGroup != null)
            return BadRequest(new { message = "Группа с таким названием уже существует", success=false });

        var group = new Group
        {
            Name = createDto.Name
        };

        await _groupRepository.AddAsync(group);
        await _redis.RemoveAsync("groups");

        return Ok(new { message = "Группа успешно создана", groupId = group.Id, success=true });
    }
    
    [HttpPut("{id}")]
    [Authorize(Roles = "Administrator")]
    public async Task<IActionResult> UpdateGroup(Guid id, [FromBody] UpdateGroupDto updateDto)
    {
        var group = await _groupRepository.GetByIdAsync(id);
        if (group == null)
            return NotFound(new { message = "Группа не найдена" });

        var existingGroup = await _groupRepository.GetByNameAsync(updateDto.Name);
        if (existingGroup != null && existingGroup.Id != id)
            return BadRequest(new { message = "Группа с таким названием уже существует" });

        group.Name = updateDto.Name;

        await _groupRepository.UpdateAsync(group);

        return Ok(new { message = "Группа успешно обновлена" });
    }
    
    [HttpDelete("{id}")]
    [Authorize(Roles = "Administrator")]
    public async Task<IActionResult> DeleteGroup(Guid id)
    {
        var group = await _groupRepository.GetByIdAsync(id);
        if (group == null)
            return NotFound(new { message = "Группа не найдена", success = false});

        if (group.Students.Any())
            return BadRequest(new { message = "Невозможно удалить группу с привязанными студентами", success = false});

        await _groupRepository.DeleteAsync(group);
        await _redis.RemoveAsync("groups");

        return Ok(new { message = "Группа успешно удалена", success = true});
    }
}