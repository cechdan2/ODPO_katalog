using PhotoApp.Models;

namespace PhotoApp.Tests.Models;

public class PhotoRecordTests
{
    [Fact]
    public void GetAdditionalPhotosList_ReturnsTrimmedPathsInOrder()
    {
        var record = new PhotoRecord
        {
            AdditionalPhotos = " /uploads/photo-1.jpg ;/uploads/photo-2.png;  /uploads/photo-3.webp  "
        };

        var photos = record.GetAdditionalPhotosList();

        Assert.Equal(
            ["/uploads/photo-1.jpg", "/uploads/photo-2.png", "/uploads/photo-3.webp"],
            photos);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ; ; ")]
    public void GetAdditionalPhotosList_ReturnsEmptyListForMissingOrBlankValues(string? additionalPhotos)
    {
        var record = new PhotoRecord
        {
            AdditionalPhotos = additionalPhotos
        };

        var photos = record.GetAdditionalPhotosList();

        Assert.Empty(photos);
    }
}
