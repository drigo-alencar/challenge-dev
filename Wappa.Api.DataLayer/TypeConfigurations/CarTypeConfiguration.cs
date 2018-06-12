﻿using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wappa.Api.DomainModel;

namespace Wappa.Api.DataLayer.TypeConfigurations
{
	class CarTypeConfiguration : IEntityTypeConfiguration<Car>
	{
		public void Configure(EntityTypeBuilder<Car> builder)
		{
			builder.ToTable("Cars");

			builder.HasKey(c => c.Id);

			builder.HasOne(c => c.Driver).WithMany(d => d.Cars).HasForeignKey(fk => fk.DriverId);

			builder.Property(c => c.LicensePlate);
			builder.Property(c => c.Model);
			builder.Property(c => c.Vendor);
		}
	}
}
