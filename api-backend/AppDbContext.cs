using Microsoft.EntityFrameworkCore;
using api_backend.Models.Auth;
using api_backend.Models.Auth.JWT;
using api_backend.Models.Dto;
using api_backend.Models.Media;
using api_backend.Models.Menus;
using api_backend.Models.Pages;
using api_backend.Models.Rendering;
using api_backend.Models.Site;
using api_backend.Services.Pages;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace api_backend;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
	public DbSet<Customer> Customers => Set<Customer>();
	public DbSet<Staff> Staff => Set<Staff>();
	public DbSet<Role> Roles => Set<Role>();
	public DbSet<StaffRole> StaffRoles => Set<StaffRole>();
	public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
	public DbSet<VerificationToken> VerificationTokens => Set<VerificationToken>();
	public DbSet<Page> Pages => Set<Page>();
	public DbSet<MediaAsset> MediaAssets => Set<MediaAsset>();
	public DbSet<SiteConfig> SiteConfig => Set<SiteConfig>();
	public DbSet<Tag> Tags => Set<Tag>();
	public DbSet<PageTag> PageTags => Set<PageTag>();
	public DbSet<Menu> Menus => Set<Menu>();
	public DbSet<RenderedPage> RenderedPages => Set<RenderedPage>();

	// camelCase in the jsonb column, matching the API and the frontend BlockInstance shape.
	private static readonly JsonSerializerOptions BlockJson = new(JsonSerializerDefaults.Web);

	protected override void OnModelCreating(ModelBuilder b)
	{
		base.OnModelCreating(b);

		b.Entity<Customer>(e =>
		{
			e.ToTable("customers");
			e.HasKey(x => x.Id);
			e.HasIndex(x => x.Email).IsUnique();
			e.Property(x => x.Email).HasMaxLength(256).IsRequired();
			e.Property(x => x.PasswordHash).IsRequired();
			e.Property(x => x.DisplayName).HasMaxLength(128);
		});

		b.Entity<Staff>(e =>
		{
			e.ToTable("staff");
			e.HasKey(x => x.Id);
			e.HasIndex(x => x.Email).IsUnique();
			e.Property(x => x.Email).HasMaxLength(256).IsRequired();
			e.Property(x => x.PasswordHash).IsRequired();
			e.Property(x => x.DisplayName).HasMaxLength(128);
			e.Property(x => x.OrganizationId).HasMaxLength(128);
			e.HasIndex(x => x.OrganizationId);
		});

		b.Entity<Role>(e =>
		{
			e.ToTable("roles");
			e.HasKey(x => x.Id);
			e.HasIndex(x => x.Name).IsUnique();
			e.Property(x => x.Name).HasMaxLength(64).IsRequired();
			e.Property(x => x.Description).HasMaxLength(256);
		});

		b.Entity<StaffRole>(e =>
		{
			e.ToTable("staff" +
				"_roles");
			e.HasKey(x => new { x.StaffId, x.RoleId });
			e.HasOne(x => x.Staff)
				.WithMany(s => s.StaffRoles)
				.HasForeignKey(x => x.StaffId)
				.OnDelete(DeleteBehavior.Cascade);
			e.HasOne(x => x.Role)
				.WithMany(r => r.StaffRoles)
				.HasForeignKey(x => x.RoleId)
				.OnDelete(DeleteBehavior.Cascade);
		});

		b.Entity<RefreshToken>(e =>
		{
			e.ToTable("refresh_tokens", t =>
				t.HasCheckConstraint(
					"ck_refresh_token_single_owner",
					"(\"CustomerId\" IS NOT NULL AND \"StaffId\" IS NULL) OR " +
					"(\"CustomerId\" IS NULL AND \"StaffId\" IS NOT NULL)"));
			e.HasKey(x => x.Id);
			e.HasIndex(x => x.TokenHash).IsUnique();
			e.Property(x => x.TokenHash).IsRequired();
			e.Ignore(x => x.IsActive);
			
			e.HasIndex(x => x.ExpiresAt); 

			e.HasOne(x => x.Customer)
				.WithMany(c => c.RefreshTokens)
				.HasForeignKey(x => x.CustomerId)
				.OnDelete(DeleteBehavior.Cascade);

			e.HasOne(x => x.Staff)
				.WithMany(s => s.RefreshTokens)
				.HasForeignKey(x => x.StaffId)
				.OnDelete(DeleteBehavior.Cascade);
		});

		b.Entity<VerificationToken>(e =>
		{
			e.ToTable("verification_tokens", t =>
				t.HasCheckConstraint(
					"ck_verification_token_single_owner",
					"(\"CustomerId\" IS NOT NULL AND \"StaffId\" IS NULL) OR " +
					"(\"CustomerId\" IS NULL AND \"StaffId\" IS NOT NULL)"));
			e.HasKey(x => x.Id);
			e.HasIndex(x => x.TokenHash).IsUnique();
			e.Property(x => x.TokenHash).IsRequired();
			e.Ignore(x => x.IsActive);
			
			e.HasIndex(x => x.ExpiresAt);

			e.HasOne(x => x.Customer)
				.WithMany(c => c.VerificationTokens)
				.HasForeignKey(x => x.CustomerId)
				.OnDelete(DeleteBehavior.Cascade);

			// No staff flows yet; FK exists for owner XOR constraint + future use.
			e.HasOne(x => x.Staff)
				.WithMany()
				.HasForeignKey(x => x.StaffId)
				.OnDelete(DeleteBehavior.Cascade);
		});

		b.Entity<Page>(e =>
		{
			e.ToTable("pages");
			e.HasKey(x => x.Id);
			e.Property(x => x.Title).HasMaxLength(200).IsRequired();
			e.Property(x => x.Slug).HasMaxLength(100).IsRequired();
			e.HasIndex(x => x.Slug).IsUnique();
			e.Property(x => x.MetaTitle).HasMaxLength(PageService.MaxMetaTitleLength).IsRequired().HasDefaultValue("");
			e.Property(x => x.MetaDescription).HasMaxLength(PageService.MaxMetaDescriptionLength).IsRequired()
				.HasDefaultValue("");
			e.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
			e.HasIndex(x => x.Status);

			// Value converter, not Npgsql dynamic JSON: keeps InMemory test provider working.
			e.Property(x => x.Blocks)
				.HasColumnType("jsonb")
				.HasConversion(
					v => JsonSerializer.Serialize(v, BlockJson),
					v => JsonSerializer.Deserialize<List<Block>>(v, BlockJson) ?? new List<Block>(),
					new ValueComparer<List<Block>>(
						(a, c) => JsonSerializer.Serialize(a, BlockJson) == JsonSerializer.Serialize(c, BlockJson),
						v => JsonSerializer.Serialize(v, BlockJson).GetHashCode(),
						v => JsonSerializer.Deserialize<List<Block>>(JsonSerializer.Serialize(v, BlockJson), BlockJson)!))
				.IsRequired();

			e.HasOne(x => x.CreatedBy)
				.WithMany()
				.HasForeignKey(x => x.CreatedById)
				.OnDelete(DeleteBehavior.SetNull);
			e.HasOne(x => x.UpdatedBy)
				.WithMany()
				.HasForeignKey(x => x.UpdatedById)
				.OnDelete(DeleteBehavior.SetNull);
		});

		b.Entity<Tag>(e =>
		{
			e.ToTable("tags");
			e.HasKey(x => x.Id);
			e.Property(x => x.Name).HasMaxLength(TagLimits.MaxLength).IsRequired();
			e.HasIndex(x => x.Name).IsUnique();
		});

		b.Entity<PageTag>(e =>
		{
			e.ToTable("page_tags");
			e.HasKey(x => new { x.PageId, x.TagId });
			e.HasIndex(x => x.TagId);
			e.HasOne(x => x.Page).WithMany(p => p.PageTags).HasForeignKey(x => x.PageId).OnDelete(DeleteBehavior.Cascade);
			e.HasOne(x => x.Tag).WithMany(t => t.PageTags).HasForeignKey(x => x.TagId).OnDelete(DeleteBehavior.Cascade);
		});

		b.Entity<Menu>(e =>
		{
			e.ToTable("menus");
			e.HasKey(x => x.Id);
			e.Property(x => x.Handle).HasMaxLength(MenuLimits.MaxHandleLength).IsRequired();
			e.HasIndex(x => x.Handle).IsUnique();
			e.Property(x => x.Items)
				.HasColumnType("jsonb")
				.HasConversion(
					v => JsonSerializer.Serialize(v, BlockJson),
					v => JsonSerializer.Deserialize<List<MenuItem>>(v, BlockJson) ?? new List<MenuItem>(),
					new ValueComparer<List<MenuItem>>(
						(a, c) => JsonSerializer.Serialize(a, BlockJson) == JsonSerializer.Serialize(c, BlockJson),
						v => JsonSerializer.Serialize(v, BlockJson).GetHashCode(),
						v => JsonSerializer.Deserialize<List<MenuItem>>(JsonSerializer.Serialize(v, BlockJson), BlockJson)!))
				.IsRequired();
			e.HasOne(x => x.UpdatedBy).WithMany().HasForeignKey(x => x.UpdatedById).OnDelete(DeleteBehavior.SetNull);
		});

		b.Entity<MediaAsset>(e =>
		{
			e.ToTable("media_assets");
			e.HasKey(x => x.Id);
			e.Property(x => x.StorageKey).HasMaxLength(64).IsRequired();
			e.HasIndex(x => x.StorageKey).IsUnique();
			e.Property(x => x.FileName).HasMaxLength(255).IsRequired();
			e.Property(x => x.ContentType).HasMaxLength(64).IsRequired();
			e.Property(x => x.Alt).HasMaxLength(MediaLimits.MaxAltLength).IsRequired();
			e.HasIndex(x => x.CreatedAt);
			e.HasOne(x => x.CreatedBy)
				.WithMany()
				.HasForeignKey(x => x.CreatedById)
				.OnDelete(DeleteBehavior.SetNull);
		});

		b.Entity<SiteConfig>(e =>
		{
			e.ToTable("site_config");
			e.HasKey(x => x.Id);
			e.Property(x => x.Id).ValueGeneratedNever();
			e.Property(x => x.Values)
				.HasColumnType("jsonb")
				.HasConversion(
					v => JsonSerializer.Serialize(v, BlockJson),
					v => JsonSerializer.Deserialize<SiteConfigValues>(v, BlockJson) ?? new SiteConfigValues(),
					new ValueComparer<SiteConfigValues>(
						(a, c) => JsonSerializer.Serialize(a, BlockJson) == JsonSerializer.Serialize(c, BlockJson),
						v => JsonSerializer.Serialize(v, BlockJson).GetHashCode(),
						v => JsonSerializer.Deserialize<SiteConfigValues>(JsonSerializer.Serialize(v, BlockJson), BlockJson)!))
				.IsRequired();
			// deleting the media just clears the slot
			e.HasOne(x => x.Logo).WithMany().HasForeignKey(x => x.LogoMediaId).OnDelete(DeleteBehavior.SetNull);
			e.HasOne(x => x.ShareImage).WithMany().HasForeignKey(x => x.ShareImageMediaId).OnDelete(DeleteBehavior.SetNull);
			e.HasOne(x => x.Icon).WithMany().HasForeignKey(x => x.IconMediaId).OnDelete(DeleteBehavior.SetNull);
			e.HasOne(x => x.UpdatedBy).WithMany().HasForeignKey(x => x.UpdatedById).OnDelete(DeleteBehavior.SetNull);
		});

		b.Entity<RenderedPage>(e =>
		{
			e.ToTable("rendered_pages");
			e.HasKey(x => x.PageId);
			e.Property(x => x.Html).IsRequired();
			e.Property(x => x.Title).HasMaxLength(200).IsRequired();
			e.Property(x => x.Slug).HasMaxLength(100).IsRequired();
			e.Property(x => x.RendererVersion).HasMaxLength(64).IsRequired();
			e.HasOne(x => x.Page).WithOne().HasForeignKey<RenderedPage>(x => x.PageId).OnDelete(DeleteBehavior.Cascade);
		});

		// Stable GUIDs → deterministic migrations.
		var superAdminId = Guid.Parse("11111111-1111-1111-1111-111111111111");
		var staffId = Guid.Parse("22222222-2222-2222-2222-222222222222");
		b.Entity<Role>().HasData(
			new Role { Id = superAdminId, Name = "super_admin", Description = "Full administrative access." },
			new Role { Id = staffId, Name = "staff", Description = "Default staff member." }
		);
	}
}