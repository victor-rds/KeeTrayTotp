﻿using KeeTrayTOTP.Libraries;
using System;
using System.Collections.Specialized;
using System.Text.RegularExpressions;
using System.Linq;

namespace KeeTrayTOTP
{
    public class KeyUri
    {
        private static string[] ValidAlgorithms = new[] { "SHA1", "SHA256", "SHA512" };
        private const string DefaultAlgorithm = "SHA1";
        private const string ValidScheme = "otpauth";
        private const int DefaultDigits = 6;
        private const int DefaultPeriod = 30;

        public KeyUri(Uri uri)
        {
            if (uri == null)
            {
                throw new ArgumentNullException("uri", "Uri should not be null.");
            }
            if (uri.Scheme != ValidScheme)
            {
                throw new ArgumentOutOfRangeException("uri", "Uri scheme must be " + ValidScheme + ".");
            }
            this.Type = EnsureValidType(uri);

            var query = ParseQueryString(uri.Query);

            // REQUIRED: The secret parameter is an arbitrary key value encoded in Base32 according to RFC 3548.
            // The padding specified in RFC 3548 section 2.2 is not required and should be omitted.
            this.Secret = EnsureValidSecret(query);
            this.Algorithm = EnsureValidAlgorithm(query);
            this.Digits = EnsureValidDigits(query);
            this.Period = EnsureValidPeriod(query);

            EnsureValidLabelAndIssuer(uri, query);
        }

        private void EnsureValidLabelAndIssuer(Uri uri, NameValueCollection query)
        {
            var label = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/'));
            if (string.IsNullOrEmpty(label))
            {
                throw new ArgumentOutOfRangeException("uri", "No label");
            }

            var labelParts = label.Split(new[] { ':' }, 2);
            if (labelParts.Length == 1)
            {
                this.Issuer = "";
                this.Label = labelParts[0];
            }
            else
            {
                Issuer = labelParts[0];
                Label = labelParts[1];
            }

            Issuer = query["issuer"] ?? Issuer;

            if (string.IsNullOrWhiteSpace(Label))
            {
                throw new ArgumentOutOfRangeException("uri", "No label");
            }
        }

        private static string EnsureValidType(Uri uri)
        {
            if (uri.Host != "totp")
            {
                throw new ArgumentOutOfRangeException("uri", "Only totp is supported.");         
            }
            return uri.Host;
        }

        private int EnsureValidDigits(NameValueCollection query)
        {
            int digits = DefaultDigits;
            if (query.AllKeys.Contains("digits") && !int.TryParse(query["digits"], out digits))
            {
                throw new ArgumentOutOfRangeException("query", "Digits not a number");
            }

            return digits;
        }

        private int EnsureValidPeriod(NameValueCollection query)
        {
            int period = DefaultPeriod;
            if (query.AllKeys.Contains("period") && !int.TryParse(query["period"], out period))
            {
                throw new ArgumentOutOfRangeException("query", "Period not a number");
            }

            return period;
        }

        private static string EnsureValidAlgorithm(NameValueCollection query)
        {
            if (query.AllKeys.Contains("algorithm") && !ValidAlgorithms.Contains(query["algorithm"]))
            {
                throw new ArgumentOutOfRangeException("query", "Not a valid algorithm");
            }

            return query["algorithm"] ?? DefaultAlgorithm;

        }

        private static string EnsureValidSecret(NameValueCollection query)
        {
            if (string.IsNullOrWhiteSpace(query["secret"]))
            {
                throw new ArgumentOutOfRangeException("query", "No secret provided.");
            }
            else if (Base32.HasInvalidPadding(query["secret"]))
            {
                throw new ArgumentOutOfRangeException("query", "Secret is not valid base32.");
            }
            else if (!Base32.IsBase32(query["secret"]))
            {
                throw new ArgumentOutOfRangeException("query", "Secret is not valid base32.");
            }

            return query["secret"].TrimEnd('=');
        }

        public string Type { get; set; }
        public string Secret { get; set; }
        public string Algorithm { get; set; }
        public int Digits { get; set; }
        public int Period { get; set; }
        public string Label { get; set; }
        public string Issuer { get; set; }

        /// <summary>
        /// Naive (and probably buggy) query string parser, but we do not want a dependency on System.Web
        /// </summary>
        private static NameValueCollection ParseQueryString(string s)
        {
            var result = new NameValueCollection();
            // remove anything other than query string from url
            s = s.Substring(s.IndexOf('?') + 1);

            foreach (var vp in Regex.Split(s, "&"))
            {
                var singlePair = Regex.Split(vp, "=");
                if (singlePair.Length == 2)
                {
                    result.Add(singlePair[0], Uri.UnescapeDataString(singlePair[1]));
                }
                else
                {
                    // only one key with no value specified in query string
                    result.Add(singlePair[0], null);
                }
            }

            return result;
        }

        public Uri GetUri()
        {
            var newQuery = new NameValueCollection();
            if (Period != 30)
            {
                newQuery["period"] = Convert.ToString(Period);
            }
            if (Digits != 6)
            {
                newQuery["digits"] = Convert.ToString(Digits);
            }
            if (Algorithm != "SHA1")
            {
                newQuery["algorithm"] = Algorithm;
            }
            newQuery["secret"] = Secret;
            newQuery["issuer"] = Issuer;

            var builder = new UriBuilder(ValidScheme, Type);
            builder.Path = "/" + Uri.EscapeDataString(Issuer) + ":" + Uri.EscapeDataString(Label);
            builder.Query = string.Join("&", newQuery.AllKeys.Select(key => key + "=" + newQuery[key]));

            return builder.Uri;
        }
    }
}
