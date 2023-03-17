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
    private readonly IMapper _mapper;
    private readonly IPhotoService _photoService;
    private readonly IUnitOfWork _uow;

    public UsersController(IUnitOfWork uow, IMapper mapper, IPhotoService photoService)
    {
        _uow = uow;
        _photoService = photoService;
        _mapper = mapper;
    }

    // [Authorize(Roles = "Admin")]
    [HttpGet]
    [Route("GetUsers")]
    public async Task<ActionResult<PagedList<AppUser>>> GetUsers([FromQuery] UserParams userParams)
    {
        var gender = await _uow.UserRepository.GetUserGender(User.GetUsername());
        userParams.CurrentUsername = User.GetUsername();

        if (string.IsNullOrEmpty(userParams.Gender))
        {
            userParams.Gender = gender == "male" ? "female" : "male";
        }

        var users = await _uow.UserRepository.GetMembersAsync(userParams);

        Response.AddPaginationHeader(new PaginationHeader(users.CurrentPage, users.PageSize, users.TotalCount, users.TotalPages));

        return Ok(users);
    }

    [Authorize(Roles = "Member")]
    [HttpGet("{username}")]
    public async Task<ActionResult<MemberDto>> GetUser(string username)
    {
        return await _uow.UserRepository.GetMemberAsync(username);
    }

    [HttpPut]
    public async Task<ActionResult> UpdateUser(MemberUpdateDto memberUpdateDto)
    {
        var user = await _uow.UserRepository.GetUserByUsernameAsync(User.GetUsername());

        if (user == null) { return NotFound(); }

        _mapper.Map(memberUpdateDto, user);

        if (await _uow.Complete()) { return NoContent(); }

        return BadRequest("Failed to update user");
    }

    [HttpPost("add-photo")]
    public async Task<ActionResult<PhotoDto>> Upload(IFormFile file)
    {
        var user = await _uow.UserRepository.GetUserByUsernameAsync(User.GetUsername());

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

        if (await _uow.Complete())
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
        var user = await _uow.UserRepository.GetUserByUsernameAsync(User.GetUsername());

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

        if (await _uow.Complete()) { return NoContent(); }

        return BadRequest("Problem setting the main photo");

    }

    [HttpDelete("delete-photo/{photoId}")]
    public async Task<IActionResult> Delete(int photoId)
    {
        BlobResponseDto response = new BlobResponseDto();
        var user = await _uow.UserRepository.GetUserByUsernameAsync(User.GetUsername());
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

        if (await _uow.Complete()) { return Ok(); }

        return BadRequest("Problem deleting photo");
    }
}
