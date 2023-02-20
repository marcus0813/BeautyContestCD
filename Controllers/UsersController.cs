using API.DTOs;
using API.Entities;
using API.Extensions;
using API.Helpers;
using API.Interface;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

[Authorize]
public class UsersController : BaseApiController
{
    private readonly IUserRepository _userRepository;
    private readonly IMapper _mapper;
    private readonly IPhotoService _photoService;

    public UsersController(IUserRepository userRepository, IMapper mapper, IPhotoService photoService)
    {
        _photoService = photoService;
        _mapper = mapper;
        _userRepository = userRepository;
    }

    [HttpGet]
    [Route("GetUsers")]
    public async Task<ActionResult<PagedList<AppUser>>> GetUsers([FromQuery] UserParams userParams)
    {
        var currentUser = await _userRepository.GetUserByUsernameAsync(User.GetUsername());
        userParams.CurrentUsername = currentUser.UserName;

        if (string.IsNullOrEmpty(userParams.Gender))
        {
            userParams.Gender = currentUser.Gender == "male" ? "female" : "male";
        }

        var users = await _userRepository.GetMembersAsync(userParams);

        Response.AddPaginationHeader(new PaginationHeader(users.CurrentPage, users.PageSize, users.TotalCount, users.TotalPage));

        return Ok(users);
    }

    [HttpGet("{username}")]
    public async Task<ActionResult<MemberDto>> GetUser(string username)
    {
        return await _userRepository.GetMemberAsync(username);
    }

    [HttpPut]
    public async Task<ActionResult> UpdateUser(MemberUpdateDto memberUpdateDto)
    {
        var user = await _userRepository.GetUserByUsernameAsync(User.GetUsername());

        if (user == null) { return NotFound(); }

        _mapper.Map(memberUpdateDto, user);

        if (await _userRepository.SaveAllAsync()) { return NoContent(); }

        return BadRequest("Failed to update user");
    }

    [HttpPost("add-photo")]
    public async Task<ActionResult<PhotoDto>> Upload(IFormFile file)
    {
        var user = await _userRepository.GetUserByUsernameAsync(User.GetUsername());

        if (user == null) { return NotFound(); }

        BlobResponseDto response = await _photoService.UploadAsync(file);

        if (response.Error == true)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, response.Status);
        }

        var photo = new Photo
        {
            Url = response.Blob.Uri
        };

        if (user.Photos.Count == 0) { photo.IsMain = true; }

        user.Photos.Add(photo);

        if (await _userRepository.SaveAllAsync())
        {
            return CreatedAtAction(nameof(GetUser),
            new { username = user.UserName },
            _mapper.Map<PhotoDto>(photo));
        }

        return BadRequest("Problem adding photo");
    }

    [HttpPut("set-main-photo/{photoId}")]
    public async Task<ActionResult> SetMainPhoto(int photoId)
    {
        var user = await _userRepository.GetUserByUsernameAsync(User.GetUsername());

        if (user == null) { return NotFound(); }

        var photo = user.Photos.FirstOrDefault(x => x.Id == photoId);

        if (photo == null) { return NotFound(); }
        if (photo.IsMain) { return BadRequest("this is already your main photo"); }

        var currentMain = user.Photos.FirstOrDefault(x => x.IsMain);
        if (currentMain != null)
        {
            currentMain.IsMain = false;
            photo.IsMain = true;
        }

        if (await _userRepository.SaveAllAsync()) { return NoContent(); }

        return BadRequest("Problem setting the main photo");

    }

    [HttpDelete("delete-photo/{photoId}")]
    public async Task<IActionResult> Delete(int photoId)
    {
        BlobResponseDto response = new BlobResponseDto();
        var user = await _userRepository.GetUserByUsernameAsync(User.GetUsername());
        var photo = user.Photos.FirstOrDefault(x => x.Id == photoId);

        if (photo == null) { return NotFound(); }

        if (photo.IsMain) { return BadRequest("You cannot delete your main photo"); }

        if (photo.Url != null)
        {
            var FileName = photo.GetFileName(photo.Url);
            response = await _photoService.DeleteAsync(FileName);
        }

        if (response.Error == true) { return BadRequest(response.Error); }

        user.Photos.Remove(photo);

        if (await _userRepository.SaveAllAsync()) { return Ok(); }

        return BadRequest("Problem deleting photo");
    }
}
