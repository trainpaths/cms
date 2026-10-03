using api_backend;
using api_backend.Models.Auth;
using Microsoft.EntityFrameworkCore;

namespace api_backend.Tests.Unit;

public static class TestDbContextFactory
{
	private static readonly Guid SuperAdminRoleId = Guid.Parse("11111111-1111-1111-1111-111111111111");
	private static readonly Guid StaffRoleId = Guid.Parse("22222222-2222-2222-2222-222222222222");

	public static AppDbContext Create(string? dbName = null)
	{
		var options = new DbContextOptionsBuilder<AppDbContext>()
			.UseInMemoryDatabase(dbName ?? Guid.NewGuid().ToString())
			.Options;

		var context = new AppDbContext(options);
		context.Database.EnsureCreated();

		if (!context.Roles.Any())
		{
			context.Roles.AddRange(
				new Role { Id = SuperAdminRoleId, Name = "super_admin", Description = "Full administrative access." },
				new Role { Id = StaffRoleId, Name = "staff", Description = "Default staff member." }
			);
			context.SaveChanges();
		}

		return context;
	}
}
