using AutoFixture;
using System;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using Wappa.Api.Controllers;
using Wappa.Api.DomainModel;
using Wappa.Api.Requests;
using Xunit;
using Microsoft.AspNetCore.Http;
using NSubstitute;
using Wappa.Api.DataLayer;
using Microsoft.AspNetCore.Mvc.Core;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using Wappa.Api.ExternalServices;
using Wappa.Api.Responses;
using System.Threading;

namespace Wappa.Api.Tests.ControllerTests
{
	public class DriversControllerTests
	{
		private static IGoogleGeocoderWrapper googleGeocoderWrapper;
		private static IUnitOfWork unitOfWork;

		private static DriversController controller;
		private static Fixture fixture;

		static DriversControllerTests()
		{
			googleGeocoderWrapper = Substitute.For<IGoogleGeocoderWrapper>();
			unitOfWork = Substitute.For<IUnitOfWork>();

			AutoMapper.Mapper.Initialize(cfg => cfg.AddProfile<MappingProfile>());

			controller = new DriversController(googleGeocoderWrapper, unitOfWork);
			fixture = new Fixture();
			RemoveThrowingBehaviorFromFixture();
		}

		private static void RemoveThrowingBehaviorFromFixture()
		{
			fixture.Behaviors.OfType<ThrowingRecursionBehavior>().ToList()
							.ForEach(b => fixture.Behaviors.Remove(b));
			fixture.Behaviors.Add(new OmitOnRecursionBehavior());
		}

		[Fact]
		public async Task When_POST_a_Driver_should_return_Created_Driver_response_Async()
		{
			//Arrange
			MockGoogleGeocoderGetAddressReturn();

			var request = fixture.Create<CreateDriverRequest>();

			//Act
			var response = await controller.Post(request);
			var result = response.Result as CreatedResult;

			//Assert
			Assert.Equal(result.StatusCode, StatusCodes.Status201Created);
			Assert.IsType<CreatedDriverResponse>(result.Value);
		}

		private static void MockGoogleGeocoderGetAddressReturn(int howManyAddressesToMock = 1)
		{
			var googleAddresses = fixture.CreateMany<GoogleAddress>(howManyAddressesToMock);
			googleGeocoderWrapper.GetAddress(Arg.Any<String>()).Returns(googleAddresses.ToList());
		}

		[Fact]
		public async Task When_POST_a_null_Driver_should_return_BadRequest()
		{
			//Arrange -> Act
			var response = await controller.Post(null);
			var result = response.Result as BadRequestResult;

			//Assert
			Assert.IsType<BadRequestResult>(result);
		}

		[Fact]
		public async Task When_POST_a_Driver_and_a_problem_occur_should_return_InternalServerError()
		{
			//Arrange
			unitOfWork.DriversRepository.When(d => d.Add(Arg.Any<Driver>())).Throw<Exception>();
			var request = fixture.Create<CreateDriverRequest>();

			//Act
			var response = await controller.Post(request);
			var result = response.Result as ObjectResult;

			//Assert
			Assert.Equal(StatusCodes.Status500InternalServerError, result.StatusCode);
		}

		[Fact]
		public async Task After_POST_a_Driver_should_have_call_SaveChanges_on_UnitOfWork()
		{
			//Arrange
			MockGoogleGeocoderGetAddressReturn();

			var request = fixture.Create<CreateDriverRequest>();

			//Act
			var response = await controller.Post(request);

			//Assert
			await unitOfWork.Received().SaveChanges();
		}

		[Fact]
		public async Task When_POST_a_Driver_must_call_GoogleGeocodeWrapper()
		{
			//Arrange
			MockGoogleGeocoderGetAddressReturn();

			var request = fixture.Create<CreateDriverRequest>();

			//Act
			var response = await controller.Post(request);

			//Assert
			await googleGeocoderWrapper.Received().GetAddress(Arg.Any<String>());
		}

		[Fact]
		public async Task When_POST_a_Driver_and_GoogleGeocodeWrapper_returns_more_than_one_address_should_return_Conflict_Status()
		{
			//Arrange
			var numberOfAddressesToMock = 5;
			MockGoogleGeocoderGetAddressReturn(numberOfAddressesToMock);

			var request = fixture.Create<CreateDriverRequest>();

			//Act
			var response = await controller.Post(request);
			var result = response.Result as ObjectResult;

			//Assert
			Assert.Equal(StatusCodes.Status409Conflict, result.StatusCode);
		}

		[Fact]
		public async Task When_DELETE_a_Driver_should_return_a_DriverResponse_with_OK_status_code()
		{
			//Arrange
			var driver = fixture.Create<Driver>();
			unitOfWork.DriversRepository.Get(Arg.Any<int>()).Returns(driver);

			//Act
			var response = await controller.Delete(driver.Id) as ActionResult<DriverResponse>;
			var result = response.Result as OkObjectResult;

			//Assert
			Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
			Assert.IsType<DriverResponse>(result.Value);
		}

		[Fact]
		public async Task When_DELETE_a_Driver_and_Id_is_invalid_should_return_a_BadRequest()
		{
			//Arrange -> Act
			var response = await controller.Delete(0);
			var result = response.Result as BadRequestObjectResult;

			//Assert
			Assert.IsType<BadRequestObjectResult>(result);
		}

		[Fact]
		public async Task When_DELETE_a_Driver_and_a_problem_occur_should_return_InternalServerError()
		{
			//Arrange
			var driverId = fixture.Create<int>();
			unitOfWork.DriversRepository.When(d => d.Get(Arg.Any<int>())).Throw<Exception>();

			//Act
			var response = await controller.Delete(driverId);
			var result = response.Result as ObjectResult;

			//Assert
			Assert.Equal(StatusCodes.Status500InternalServerError, result.StatusCode);
		}

		[Fact]
		public async Task When_try_to_DELETE_an_already_deleted_Driver_should_return_NotFound()
		{
			//Arrange
			var driverId = fixture.Create<int>();
			unitOfWork.DriversRepository.Get(Arg.Any<int>()).Returns(default(Driver));

			//Act
			var response = await controller.Delete(driverId);
			var result = response.Result as ObjectResult;

			//Assert
			Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
		}

		[Fact]
		public async Task When_DELETE_a_Driver_should_call_SaveChange_on_UnitOfWork()
		{
			//Arrange
			var driver = fixture.Create<Driver>();
			unitOfWork.DriversRepository.Get(Arg.Any<int>()).Returns(driver);

			//Act
			var response = await controller.Delete(driver.Id);

			//Assert
			await unitOfWork.Received().SaveChanges();
		}

		[Fact]
		public async Task When_GET_all_Drivers_should_return_a_list_with_OK_status_code()
		{
			//Arrange
			var howManyDriversToCreate = 50;
			var drivers = new List<Driver>(fixture.CreateMany<Driver>(howManyDriversToCreate));

			unitOfWork.DriversRepository.GetAll(Arg.Any<String>(), Arg.Any<int>(), Arg.Any<int>())
				.Returns(drivers);

			//Act
			var response = await controller.Get() as ActionResult<List<Driver>>;
			var result = response.Result as OkObjectResult;

			//Assert
			Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
			Assert.IsType<List<DriverResponse>>(result.Value);
		}

		[Fact]
		public async Task When_GET_all_Drivers_and_a_problem_occur_should_return_InternalServerError()
		{
			//Arrange
			unitOfWork.DriversRepository.When(d => d.GetAll(Arg.Any<String>(), Arg.Any<int>(), Arg.Any<int>())).Throw<Exception>();

			//Act
			var response = await controller.Get();
			var result = response.Result as ObjectResult;

			//Assert
			Assert.Equal(StatusCodes.Status500InternalServerError, result.StatusCode);
		}

		[Fact]
		public async Task When_GET_all_Drivers_and_SortBy_query_parameter_is_not_FirstName_or_LastName_should_return_BadRequest()
		{
			//Arrange -> Act
			var response = await controller.Get(sortBy: fixture.Create<String>());
			var result = response.Result as BadRequestObjectResult;

			//Assert
			Assert.IsType<BadRequestObjectResult>(result);
		}

		[Fact]
		public async Task When_GET_all_Drivers_and_SortBy_is_FirstName_or_LastName_should_return_a_list_with_OK_status_code()
		{
			//Arrange
			var options = new String[] { "FirstName", "LastName" };
			var randomOption = options[new Random().Next(2)];

			var howManyDriversToCreate = 10;
			var drivers = new List<Driver>(fixture.CreateMany<Driver>(howManyDriversToCreate));

			unitOfWork.DriversRepository.GetAll(Arg.Any<String>(), Arg.Any<int>(), Arg.Any<int>())
				.Returns(drivers);

			//Act
			var response = await controller.Get(sortBy: randomOption) as ActionResult<List<Driver>>;
			var result = response.Result as OkObjectResult;

			//Assert
			Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
			Assert.IsType<List<DriverResponse>>(result.Value);
		}

		[Fact]
		public async Task When_GET_all_Drivers_should_not_matter_how_SortBy_is_written_and_must_return_a_list_with_OK_status_code()
		{
			//Arrange
			var options = new String[] { "firStnamE", "lAstnAme" };
			var randomOption = options[new Random().Next(2)];

			var howManyDriversToCreate = 10;
			var drivers = new List<Driver>(fixture.CreateMany<Driver>(howManyDriversToCreate));

			unitOfWork.DriversRepository.GetAll(Arg.Any<String>(), Arg.Any<int>(), Arg.Any<int>())
				.Returns(drivers);

			//Act
			var response = await controller.Get(sortBy: randomOption) as ActionResult<List<Driver>>;
			var result = response.Result as OkObjectResult;

			//Assert
			Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
			Assert.IsType<List<DriverResponse>>(result.Value);
		}

		[Fact]
		public async Task When_GET_a_Driver_by_Id_should_return_a_DriverResponse()
		{
			//Arrange
			var driver = fixture.Create<Driver>();
			unitOfWork.DriversRepository.Get(Arg.Any<int>()).Returns(driver);

			//Act
			var response = await controller.Get(driver.Id) as ActionResult<DriverResponse>;
			var result = response.Result as OkObjectResult;

			//Assert
			Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
			Assert.IsType<DriverResponse>(result.Value);
		}

		[Fact]
		public async Task When_GET_a_specific_Driver_and_Id_is_invalid_should_return_a_BadRequest()
		{
			//Arrange -> Act
			var response = await controller.Get(0);
			var result = response.Result as BadRequestObjectResult;

			//Assert
			Assert.IsType<BadRequestObjectResult>(result);
		}

		[Fact]
		public async Task When_GET_a_specific_Driver_and_a_problem_occur_should_return_a_InternalServerError()
		{
			//Arrange
			unitOfWork.DriversRepository.When(d => d.Get(Arg.Any<int>())).Throw<Exception>();

			//Act
			var response = await controller.Get(10);
			var result = response.Result as ObjectResult;

			//Assert
			Assert.Equal(StatusCodes.Status500InternalServerError, result.StatusCode);
		}

		[Fact]
		public async Task When_GET_a_Driver_Address_should_return_a_Address_as_response_with_Ok_status_code()
		{
			//Arrange
			var driver = fixture.Create<Driver>();
			unitOfWork.DriversRepository.Get(Arg.Any<int>()).Returns(driver);

			//Act
			var response = await controller.GetDriverAddress(driver.Id) as ActionResult<Models.Address>;
			var result = response.Result as OkObjectResult;

			//Assert
			Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
			Assert.IsType<Models.Address>(result.Value);
		}

		[Fact]
		public async Task When_GET_a_Driver_Address_and_Id_is_invalid_should_return_a_BadRequest()
		{
			//Arrange -> Act
			var response = await controller.GetDriverAddress(0);
			var result = response.Result as BadRequestObjectResult;

			//Assert
			Assert.IsType<BadRequestObjectResult>(result);
		}

		[Fact]
		public async Task When_GET_a_Driver_Address_and_it_not_exist_should_return_a_NoContent()
		{
			//Arrange
			var driverId = fixture.Create<int>();
			unitOfWork.DriversRepository.Get(Arg.Any<int>()).Returns(default(Driver));

			//Act
			var response = await controller.GetDriverAddress(driverId) as ActionResult<Models.Address>;
			var result = response.Result as NoContentResult;

			//Assert
			Assert.Equal(StatusCodes.Status204NoContent, result.StatusCode);
		}

		[Fact]
		public async Task When_GET_a_Driver_Address_and_a_problem_occur_should_return_a_InternalServerError()
		{
			//Arrange
			unitOfWork.DriversRepository.When(d => d.Get(Arg.Any<int>())).Throw<Exception>();
			var driverId = fixture.Create<int>();

			//Act
			var response = await controller.GetDriverAddress(driverId);
			var result = response.Result as ObjectResult;

			//Assert
			Assert.Equal(StatusCodes.Status500InternalServerError, result.StatusCode);
			Assert.IsType<ObjectResult>(result);
		}

		[Fact]
		public async Task When_GET_a_Driver_Cars_should_return_a_CarResponse_as_response_with_Ok_status_code()
		{
			//Arrange
			var driver = fixture.Create<Driver>();
			unitOfWork.DriversRepository.Get(Arg.Any<int>()).Returns(driver);

			//Act
			var response = await controller.GetDriverCars(driver.Id) as ActionResult<List<Models.Car>>;
			var result = response.Result as OkObjectResult;

			//Assert
			Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
			Assert.IsType<List<Models.Car>>(result.Value);
		}

		[Fact]
		public async Task When_GET_a_Driver_Cars_and_Id_is_invalid_should_return_a_BadRequest()
		{
			//Arrange -> Act
			var response = await controller.GetDriverAddress(0);
			var result = response.Result as BadRequestObjectResult;

			//Assert
			Assert.IsType<BadRequestObjectResult>(result);
		}

		[Fact]
		public async Task When_GET_a_Driver_Cars_and_it_not_exist_should_return_a_NoContent()
		{
			//Arrange
			var driverId = fixture.Create<int>();
			unitOfWork.DriversRepository.Get(Arg.Any<int>()).Returns(default(Driver));

			//Act
			var response = await controller.GetDriverCars(driverId) as ActionResult<List<Models.Car>>;
			var result = response.Result as NoContentResult;

			//Assert
			Assert.Equal(StatusCodes.Status204NoContent, result.StatusCode);
		}

		[Fact]
		public async Task When_GET_a_Driver_Cars_and_a_problem_occur_should_return_a_InternalServerError()
		{
			//Arrange
			unitOfWork.DriversRepository.When(d => d.Get(Arg.Any<int>())).Throw<Exception>();
			var driverId = fixture.Create<int>();

			//Act
			var response = await controller.GetDriverAddress(driverId);
			var result = response.Result as ObjectResult;

			//Assert
			Assert.Equal(StatusCodes.Status500InternalServerError, result.StatusCode);
			Assert.IsType<ObjectResult>(result);
		}

		[Fact]
		public async Task When_PUT_a_Driver_should_return_an_updated_DriverResponse_with_Ok_status_code()
		{
			//Arrange
			var updatedDriver = fixture.Create<UpdateDriverRequest>();
			MockGoogleGeocoderGetAddressReturn();

			//Act
			var response = await controller.Put(updatedDriver) as ActionResult<DriverResponse>;
			var result = response.Result as OkObjectResult;

			//Assert
			Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
			Assert.IsType<DriverResponse>(result.Value);
		}

		[Fact]
		public async Task When_PUT_a_Driver_and_request_is_invalid_should_return_a_BadRequest()
		{
			//Arrange -> Act
			var response = await controller.Put(null);
			var result = response.Result as BadRequestObjectResult;

			//Assert
			Assert.IsType<BadRequestObjectResult>(result);
		}

		[Fact]
		public async Task When_PUT_a_Driver_and_request_Car_list_is_null_should_return_a_BadRequest()
		{
			//Arrange
			var updatedDriver = fixture.Create<UpdateDriverRequest>();
			updatedDriver.Cars = null;

			MockGoogleGeocoderGetAddressReturn();

			//Act
			var response = await controller.Put(updatedDriver);
			var result = response.Result as BadRequestObjectResult;

			//Assert
			Assert.IsType<BadRequestObjectResult>(result);
		}

		[Fact]
		public async Task When_PUT_a_Driver_and_request_Car_list_has_Zero_elements_should_return_a_BadRequest()
		{
			//Arrange
			var updatedDriver = fixture.Create<UpdateDriverRequest>();
			updatedDriver.Cars = new List<Models.Car>();

			MockGoogleGeocoderGetAddressReturn();

			//Act
			var response = await controller.Put(updatedDriver);
			var result = response.Result as BadRequestObjectResult;

			//Assert
			Assert.IsType<BadRequestObjectResult>(result);
		}

		[Fact]
		public async Task When_PUT_a_Driver_and_a_problem_occur_should_return_InternalServerError()
		{
			//Arrange
			var updatedDriver = fixture.Create<UpdateDriverRequest>();
			unitOfWork.DriversRepository.When(d => d.Update(Arg.Any<Driver>())).Throw<Exception>();

			//Act
			var response = await controller.Put(updatedDriver);
			var result = response.Result as ObjectResult;

			//Assert
			Assert.Equal(StatusCodes.Status500InternalServerError, result.StatusCode);
		}

		[Fact]
		public async Task When_try_to_PUT_an_already_deleted_Driver_should_return_InternalServerError()
		{
			//Arrange
			var updatedDriver = fixture.Create<UpdateDriverRequest>();
			unitOfWork.DriversRepository.Update(Arg.Any<Driver>()).Returns(default(Task));

			//Act
			var response = await controller.Put(updatedDriver);
			var result = response.Result as ObjectResult;

			//Assert
			Assert.Equal(StatusCodes.Status500InternalServerError, result.StatusCode);
		}

		[Fact]
		public async Task When_PUT_a_Driver_should_call_SaveChange_on_UnitOfWork()
		{
			//Arrange
			var updatedDriver = fixture.Create<UpdateDriverRequest>();
			MockGoogleGeocoderGetAddressReturn();

			//Act
			var response = await controller.Put(updatedDriver);

			//Assert
			await unitOfWork.Received().SaveChanges();
		}

		[Fact]
		public async Task When_PUT_a_Driver_should_call_GetAddress_on_GoogleGeocoderWrapper()
		{
			//Arrange
			var updatedDriver = fixture.Create<UpdateDriverRequest>();

			//Act
			var response = await controller.Put(updatedDriver);

			//Assert
			await googleGeocoderWrapper.Received().GetAddress(Arg.Any<String>());
		}

		[Fact]
		public async Task When_PUT_a_Driver_Address_should_return_an_updated_Address_with_Ok_status_code()
		{
			//Arrange
			var updatedDriverAddress = fixture.Create<UpdateDriverAddressRequest>();
			MockGoogleGeocoderGetAddressReturn();

			//Act
			var response = await controller.PutAddress(updatedDriverAddress) as ActionResult<Models.Address>;
			var result = response.Result as OkObjectResult;

			//Assert
			Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
			Assert.IsType<Models.Address>(result.Value);
		}

		[Fact]
		public async Task When_PUT_a_Driver_Address_and_request_is_invalid_should_return_a_BadRequest()
		{
			//Arrange -> Act
			var response = await controller.PutAddress(null);
			var result = response.Result as BadRequestObjectResult;

			//Assert
			Assert.IsType<BadRequestObjectResult>(result);
		}

		[Fact]
		public async Task When_PUT_a_Driver_Address_and_a_problem_occur_should_return_InternalServerError()
		{
			//Arrange
			var updatedDriverAddress = fixture.Create<UpdateDriverAddressRequest>();
			unitOfWork.AddressRepository.When(d => d.Update(Arg.Any<Address>())).Throw<Exception>();

			//Act
			var response = await controller.PutAddress(updatedDriverAddress);
			var result = response.Result as ObjectResult;

			//Assert
			Assert.Equal(StatusCodes.Status500InternalServerError, result.StatusCode);
		}

		[Fact]
		public async Task When_PUT_a_Driver_Address_should_call_SaveChange_on_UnitOfWork()
		{
			//Arrange
			var updatedDriverAddress = fixture.Create<UpdateDriverAddressRequest>();
			MockGoogleGeocoderGetAddressReturn();

			//Act
			var response = await controller.PutAddress(updatedDriverAddress);

			//Assert
			await unitOfWork.Received().SaveChanges();
		}

		[Fact]
		public async Task When_PUT_a_Driver_Address_should_call_GetAddress_GoogleGeocoderWrapper()
		{
			//Arrange
			var updatedDriverAddress = fixture.Create<UpdateDriverAddressRequest>();

			//Act
			var response = await controller.PutAddress(updatedDriverAddress);

			//Assert
			await googleGeocoderWrapper.Received().GetAddress(Arg.Any<String>());
		}

		[Fact]
		public async Task When_PUT_a_Driver_Cars_should_return_an_updated_CarList_with_Ok_status_code()
		{
			//Arrange
			var updatedDriverCars = fixture.CreateMany<UpdateDriverCarRequest>().ToList();

			//Act
			var response = await controller.PutCar(updatedDriverCars) as ActionResult<List<Models.Car>>;
			var result = response.Result as OkObjectResult;

			//Assert
			Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
			Assert.IsType<List<Models.Car>>(result.Value);
		}

		[Fact]
		public async Task When_PUT_a_Driver_Cars_and_request_is_invalid_should_return_a_BadRequest()
		{
			//Arrange -> Act
			var response = await controller.PutCar(null);
			var result = response.Result as BadRequestObjectResult;

			//Assert
			Assert.IsType<BadRequestObjectResult>(result);
		}

		[Fact]
		public async Task When_PUT_a_Driver_Cars_and_request_Cars_is_null_should_return_a_BadRequest()
		{
			//Arrange
			var updatedDriverCars = default(List<UpdateDriverCarRequest>);

			MockGoogleGeocoderGetAddressReturn();

			//Act
			var response = await controller.PutCar(updatedDriverCars);
			var result = response.Result as BadRequestObjectResult;

			//Assert
			Assert.IsType<BadRequestObjectResult>(result);
		}

		[Fact]
		public async Task When_PUT_a_Driver_Cars_and_a_problem_occur_should_return_InternalServerError()
		{
			//Arrange
			var updatedDriverCars = fixture.CreateMany<UpdateDriverCarRequest>().ToList();
			unitOfWork.CarRepository.When(d => d.Update(Arg.Any<List<Car>>())).Throw<Exception>();

			//Act
			var response = await controller.PutCar(updatedDriverCars);
			var result = response.Result as ObjectResult;

			//Assert
			Assert.Equal(StatusCodes.Status500InternalServerError, result.StatusCode);
		}

		[Fact]
		public async Task When_PUT_a_Driver_Cars_should_call_SaveChange_on_UnitOfWork()
		{
			//Arrange
			var updatedDriverCars = fixture.CreateMany<UpdateDriverCarRequest>().ToList();

			//Act
			var response = await controller.PutCar(updatedDriverCars);

			//Assert
			await unitOfWork.Received().SaveChanges();
		}
	}
}
