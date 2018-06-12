﻿using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Wappa.Api.ExternalServices.Exceptions;

namespace Wappa.Api.ExternalServices
{
	public class GoogleGeocoderWrapper : IGoogleGeocoderWrapper
	{
		private const string API_SETTINGS_SECTION_NAME = "APIs";
		private const string GOOGLE_API_SETTINGS_SECTION_NAME = "Google_Geocoding_API";
		private const string URI_SETTING_KEY = "Uri";
		private const string API_SETTING_KEY = "Key";

		private String gooogleGeocodingAPIUri;
		private String gooogleGeocodingAPIKey;

		private HttpClient client;

		public GoogleGeocoderWrapper(IConfiguration configuration)
		{
			var googleApiSettings = configuration.GetSection(API_SETTINGS_SECTION_NAME).GetSection(GOOGLE_API_SETTINGS_SECTION_NAME);
			this.gooogleGeocodingAPIUri = googleApiSettings[URI_SETTING_KEY];
			this.gooogleGeocodingAPIKey = googleApiSettings[API_SETTING_KEY];

			this.client = new HttpClient();
		}

		public async Task<GoogleAddress> GetAddress(String addressToSearch)
		{
			if (String.IsNullOrEmpty(addressToSearch.Trim())) { throw new ArgumentNullException(nameof(addressToSearch)); }

			var addressSearchUri = BuildAddressSearchUri(addressToSearch);

			var response = await this.client.GetAsync(addressSearchUri);
			var content = await response.Content.ReadAsStringAsync();

			var addresses = DeserializeReceivedGoogleAddresses(content);

			if (HasNotFoundAnyAddress(addresses))
			{
				throw new AddressNotFoundException($"The following address was not found: {addressToSearch}");
			}

			if (HasNotAbleToFindOnlyOneAddress(addresses))
			{
				throw new AddressNotFoundException(addressToSearch, addresses);
			}

			return addresses.First();
		}

		private Uri BuildAddressSearchUri(string address)
		{
			var builder = new UriBuilder(this.gooogleGeocodingAPIUri);
			builder.Query = $"address={address}&key={gooogleGeocodingAPIKey}";

			return builder.Uri;
		}

		private static IList<GoogleAddress> DeserializeReceivedGoogleAddresses(string content)
		{
			var resultsToken = JObject.Parse(content).SelectToken("results");

			var deserializedGoogleAddreses = resultsToken.Select(r =>
			{
				return GoogleAddressFromJson(r);
			});

			return deserializedGoogleAddreses.ToList();
		}

		private static GoogleAddress GoogleAddressFromJson(JToken r)
		{
			var address = r.ToObject<GoogleAddress>();

			var geometryToken = r.SelectToken("geometry");
			var locationToken = geometryToken.SelectToken("location");

			address.Location = locationToken.ToObject<Location>();

			return address;
		}

		private static bool HasNotFoundAnyAddress(IList<GoogleAddress> addresses)
		{
			return addresses.Count == 0;
		}

		private bool HasNotAbleToFindOnlyOneAddress(IList<GoogleAddress> addresses)
		{
			if (addresses.Count > 1) { return true; }

			return false;
		}
	}
}
