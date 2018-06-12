﻿using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Wappa.Api.DomainModel;

namespace Wappa.Api.DataLayer.Repositories
{
	public class DriversRepository : IDriversRepository
	{
		private static readonly String LAST_NAME_SORT_FILTER = "lastname";

		private BackOfficeContext context;

		public DriversRepository(BackOfficeContext context)
		{
			this.context = context;
		}

		public void Add(Driver driver)
		{
			this.context.Drivers.Add(driver);
		}

		public async Task<Driver> Get(int id)
		{
			return await Task.FromResult(this.context.Drivers.Include(a => a.Address).Include(c => c.Cars).FirstOrDefault(d => d.Id == id));
		}

		public async Task<IList<Driver>> GetAll(String sortBy, int limit, int offset)
		{
			var sortedDrivers = default(IQueryable<Driver>);
			var drivers = default(IList<Driver>);

			if (sortBy == LAST_NAME_SORT_FILTER)
			{
				sortedDrivers = this.SortDriversByLastName();
				drivers = this.ApplyLimitAndOffset(sortedDrivers, limit, offset);
				return await Task.FromResult(drivers);
			}

			sortedDrivers = this.SortDriversByFirstName();
			drivers = this.ApplyLimitAndOffset(sortedDrivers, limit, offset);

			return await Task.FromResult(drivers);
		}

		private IQueryable<Driver> SortDriversByLastName()
		{
			return this.context.Drivers.Include(a => a.Address).Include(c => c.Cars)
				.OrderBy(d => d.LastName);
		}

		private IList<Driver> ApplyLimitAndOffset(IQueryable<Driver> drivers, int limit, int offset)
		{
			return drivers.Skip(offset).Take(limit).ToList();
		}

		private IQueryable<Driver> SortDriversByFirstName()
		{
			return this.context.Drivers.Include(a => a.Address).Include(c => c.Cars)
				.OrderBy(d => d.FirstName);
		}

		public async Task Delete(Driver driver)
		{
			await Task.FromResult(this.context.Drivers.Remove(driver));
		}

		public async Task Update(Driver driver)
		{
			await Task.Factory.StartNew(() =>
			{
				this.UpdateDriver(driver);

				this.UpdateDriverAddress(driver);

				this.UpdateDriverCars(driver);
			});
		}

		private void UpdateDriver(Driver driver)
		{
			var entity = this.context.Drivers.Find(driver.Id);
			driver.Id = entity.Id;
			this.context.Entry(entity).CurrentValues.SetValues(driver);
		}

		private void UpdateDriverAddress(Driver driver)
		{
			var entity = this.context.Addresses.Find(driver.Address.Id);
			driver.Address.DriverId = entity.DriverId;
			this.context.Entry(entity).CurrentValues.SetValues(driver.Address);
		}

		private void UpdateDriverCars(Driver driver)
		{
			foreach (var car in driver.Cars)
			{
				var entity = this.context.Cars.Find(car.Id);
				car.DriverId = entity.DriverId;
				this.context.Entry(entity).CurrentValues.SetValues(car);
			}
		}
	}
}
