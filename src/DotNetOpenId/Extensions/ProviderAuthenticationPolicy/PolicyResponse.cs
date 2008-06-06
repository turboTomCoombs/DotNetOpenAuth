﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using System.Globalization;

namespace DotNetOpenId.Extensions.ProviderAuthenticationPolicy {
	/// <summary>
	/// The PAPE response part of an OpenID Authentication response message.
	/// </summary>
	public sealed class PolicyResponse : IExtensionResponse {
		// This array of formats is not yet a complete list.
		static readonly string[] PermissibleDateTimeFormats = { "yyyy-MM-ddTHH:mm:ssZ" };

		/// <summary>
		/// Instantiates a <see cref="PolicyResponse"/>.
		/// </summary>
		public PolicyResponse() {
			ActualPolicies = new List<string>(1);
		}

		/// <summary>
		/// One or more authentication policy URIs that the OP conformed to when authenticating the End User.
		/// </summary>
		/// <remarks>
		/// If no policies were met though the OP wishes to convey other information in the response, this parameter MUST be included with the value of "none".
		/// </remarks>
		public IList<string> ActualPolicies { get; private set; }
		DateTime? authenticationTimeUtc;
		/// <summary>
		/// Optional. The most recent timestamp when the End User has actively authenticated to the OP in a manner fitting the asserted policies.
		/// </summary>
		/// <remarks>
		/// If the RP's request included the "openid.max_auth_age" parameter then the OP MUST include "openid.auth_time" in its response. If "openid.max_auth_age" was not requested, the OP MAY choose to include "openid.auth_time" in its response.
		/// </remarks>
		public DateTime? AuthenticationTimeUtc {
			get { return authenticationTimeUtc; }
			set {
				// Make sure that whatever is set here, it becomes UTC time.
				if (value.HasValue) {
					if (value.Value.Kind == DateTimeKind.Unspecified)
						throw new ArgumentException(Strings.UnspecifiedDateTimeKindNotAllowed, "value");
					authenticationTimeUtc = value.Value.ToUniversalTime();
				} else {
					authenticationTimeUtc = null;
				}
			}
		}
		/// <summary>
		/// Optional. The Assurance Level as defined by the National Institute of Standards and Technology (NIST) in Special Publication 800-63 (Burr, W., Dodson, D., and W. Polk, Ed., “Electronic Authentication Guideline,” April 2006.) [NIST_SP800‑63] corresponding to the authentication method and policies employed by the OP when authenticating the End User.
		/// </summary>
		/// <remarks>
		/// See PAPE spec Appendix A.1.2 (NIST Assurance Levels) for high-level example classifications of authentication methods within the defined levels.
		/// </remarks>
		public NistAssuranceLevel? NistAssuranceLevel { get; set; }

		/// <summary>
		/// Tests equality between two <see cref="PolicyResponse"/> instances.
		/// </summary>
		public override bool Equals(object obj) {
			PolicyResponse other = obj as PolicyResponse;
			if (other == null) return false;
			if (AuthenticationTimeUtc != other.AuthenticationTimeUtc) return false;
			if (NistAssuranceLevel != other.NistAssuranceLevel) return false;
			if (ActualPolicies.Count != other.ActualPolicies.Count) return false;
			foreach (string policy in ActualPolicies) {
				if (!other.ActualPolicies.Contains(policy)) return false;
			}
			return true;
		}

		/// <summary>
		/// Gets a hash code for this object.
		/// </summary>
		public override int GetHashCode() {
			return ActualPolicies.GetHashCode();
		}

		#region IExtensionResponse Members

		IDictionary<string, string> IExtensionResponse.Serialize(DotNetOpenId.Provider.IRequest authenticationRequest) {
			var fields = new Dictionary<string, string>();

			fields.Add(Constants.ResponseParameters.AuthPolicies, PolicyRequest.SerializePolicies(ActualPolicies));
			if (AuthenticationTimeUtc.HasValue) {
				fields.Add(Constants.ResponseParameters.AuthTime, AuthenticationTimeUtc.Value.ToUniversalTime().ToString(PermissibleDateTimeFormats[0], CultureInfo.InvariantCulture));
			}
			if (NistAssuranceLevel.HasValue) {
				fields.Add(Constants.ResponseParameters.NistAuthLevel, ((int)NistAssuranceLevel).ToString(CultureInfo.InvariantCulture));
			}

			return fields;
		}

		bool IExtensionResponse.Deserialize(IDictionary<string, string> fields, DotNetOpenId.RelyingParty.IAuthenticationResponse response, string typeUri) {
			if (fields == null) return false;
			if (!fields.ContainsKey(Constants.ResponseParameters.AuthPolicies)) return false;

			ActualPolicies.Clear();
			string[] actualPolicies = fields[Constants.ResponseParameters.AuthPolicies].Split(' ');
			foreach (string policy in actualPolicies) {
				if (policy.Length > 0)
					ActualPolicies.Add(policy);
			}

			AuthenticationTimeUtc = null;
			string authTime;
			if (fields.TryGetValue(Constants.ResponseParameters.AuthTime, out authTime)) {
				DateTime authDateTime;
				if (DateTime.TryParse(authTime, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out authDateTime) &&
					authDateTime.Kind == DateTimeKind.Utc) { // may be unspecified per our option above
					AuthenticationTimeUtc = authDateTime;
				} else {
					if (TraceUtil.Switch.TraceError)
						Trace.TraceError("Invalid format for {0} parameter: {1}",
							Constants.ResponseParameters.AuthTime, authTime);
				}
			}

			NistAssuranceLevel = null;
			string nistAuthLevel;
			if (fields.TryGetValue(Constants.ResponseParameters.NistAuthLevel, out nistAuthLevel)) {
				int nistAuthLevelNumber;
				if (int.TryParse(nistAuthLevel, out nistAuthLevelNumber) &&
					nistAuthLevelNumber >= 0 && nistAuthLevelNumber <= 4) {
					NistAssuranceLevel = (NistAssuranceLevel)nistAuthLevelNumber;
				} else {
					if (TraceUtil.Switch.TraceError)
						Trace.TraceError("Invalid NIST level.");
				}
			}

			return true;
		}

		#endregion

		#region IExtension Members

		string IExtension.TypeUri {
			get { return Constants.TypeUri; }
		}

		IEnumerable<string> IExtension.AdditionalSupportedTypeUris {
			get { return new string[0]; }
		}

		#endregion
	}
}
