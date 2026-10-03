using api_backend.Models.Auth;
using api_backend.Models.Auth.JWT;
using api_backend.Models.Dto;
using api_backend.Services.Auth.JWT;
using Microsoft.EntityFrameworkCore;

namespace api_backend.Services.Auth;

public class CustomerAuthService(
	AppDbContext db,
	PasswordHashService hasher,
	JwtTokenService tokens,
	RefreshTokenService refreshTokens)
	: PrincipalAuthService<Customer>(db, hasher, tokens, refreshTokens)
{
	protected override UserType Type => UserType.Customer;

	protected override UserInfo ToUserInfo(Customer customer) =>
		new(customer.Id, customer.Email, customer.DisplayName, AuthClaims.CustomerUserType, [], null);

	protected override AccessToken CreateAccessToken(Customer customer) =>
		Tokens.CreateAccessToken(customer.Id, customer.Email, AuthClaims.CustomerUserType, roles: []);

	public async Task<AuthResult?> RegisterAsync(RegisterRequest req, string? ip, CancellationToken ct = default)
	{
		var email = NormalizeEmail(req.Email);
		if (await Db.Customers.AnyAsync(c => c.Email == email, ct))
			return null;

		var customer = new Customer
		{
			Email = email,
			PasswordHash = Hasher.Hash(req.Password),
			DisplayName = req.DisplayName,
		};
		Db.Customers.Add(customer);

		try
		{
			return await IssueAsync(customer, ip, ct);
		}
		catch (DbUpdateException ex) when (IsUniqueViolation(ex))
		{
			return null; // concurrent registration of the same email
		}
	}
}
