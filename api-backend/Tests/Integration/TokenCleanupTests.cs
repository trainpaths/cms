using api_backend.Models.Auth;
using api_backend.Models.Auth.JWT;
using api_backend.Services.Auth;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace api_backend.Tests.Integration;

public class TokenCleanupTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
	private static CancellationToken Ct => TestContext.Current.CancellationToken;

	[Fact]
	public async Task PurgeAsync_DeletesOnlyTokensPastRetention()
	{
		using var scope = factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
		var now = DateTimeOffset.UtcNow;
		var old = now - TokenCleanupService.Retention - TimeSpan.FromDays(1);

		var customer = new Customer { Email = $"cleanup-{Guid.NewGuid()}@test.com", PasswordHash = "x" };
		db.Customers.Add(customer);
		RefreshToken Refresh(DateTimeOffset expires, DateTimeOffset? revoked = null) => new()
		{
			TokenHash = Guid.NewGuid().ToString(), UserType = UserType.Customer, CustomerId = customer.Id,
			ExpiresAt = expires, RevokedAt = revoked,
		};
		var expired = Refresh(old);
		var revokedLongAgo = Refresh(now.AddDays(10), old);
		var active = Refresh(now.AddDays(10));
		var recentlyRevoked = Refresh(now.AddDays(10), now);
		db.RefreshTokens.AddRange(expired, revokedLongAgo, active, recentlyRevoked);
		db.VerificationTokens.Add(new VerificationToken
		{
			TokenHash = Guid.NewGuid().ToString(), CustomerId = customer.Id, ExpiresAt = old,
		});
		await db.SaveChangesAsync(Ct);

		await TokenCleanupService.PurgeAsync(db, now, Ct);

		var remaining = await db.RefreshTokens.AsNoTracking()
			.Where(t => t.CustomerId == customer.Id).Select(t => t.Id).ToListAsync(Ct);
		remaining.Should().BeEquivalentTo(new[] { active.Id, recentlyRevoked.Id });
		(await db.VerificationTokens.AnyAsync(t => t.CustomerId == customer.Id, Ct)).Should().BeFalse();
	}
}
