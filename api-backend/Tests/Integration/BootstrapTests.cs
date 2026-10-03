using System.Net;
using System.Net.Http.Json;
using api_backend.Models.Dto;
using AwesomeAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace api_backend.Tests.Integration;

public class BootstrapTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
	private static CancellationToken Ct => TestContext.Current.CancellationToken;

	[Fact]
	public async Task SuperAdmin_IsCreatedOnceFromConfig()
	{
		const string email = "bootstrap-admin@test.com";
		const string password = "bootstrap-password-1";

		for (var i = 0; i < 2; i++)
		{
			await using var app = factory.WithWebHostBuilder(b => b
				.UseSetting("Bootstrap:SuperAdminEmail", email)
				.UseSetting("Bootstrap:SuperAdminPassword", password));
			using var client = app.CreateCookielessClient();

			var login = await client.PostAsJsonAsync("/api/auth/staff/login", new LoginRequest(email, password), Ct);
			login.StatusCode.Should().Be(HttpStatusCode.OK);
			(await login.Content.ReadFromJsonAsync<AuthResponse>(Ct))!.User.Roles.Should().Contain("super_admin");
		}

		using var scope = factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
		(await db.Staff.CountAsync(s => s.Email == email, Ct)).Should().Be(1);
	}
}
