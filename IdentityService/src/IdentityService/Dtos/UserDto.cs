namespace IdentityService.Dtos;

public record UserDto(
    string        Id,
    string        Email,
    string?       FirstName,
    string?       LastName,
    List<string>  Roles
);