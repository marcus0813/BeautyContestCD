using API.DTOs;
using Azure;

namespace API.Interface
{
    public interface IPhotoService
    {
        Task<BlobResponseDto> UploadAsync(IFormFile file);
        Task<BlobResponseDto> DeleteAsync(string blobFilename);
        Task<List<BlobDto>> ListAsync();
    }
}