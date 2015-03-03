﻿/* 
 * Copyright (c) 2014, Furore (info@furore.com) and contributors
 * See the file CONTRIBUTORS for details.
 * 
 * This file is licensed under the BSD 3-Clause license
 * available at https://raw.githubusercontent.com/ewoutkramer/fhir-net-api/master/LICENSE
 */

using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using Hl7.Fhir.Serialization;
using Hl7.Fhir.Support;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;

namespace Hl7.Fhir.Rest
{
    internal static class HttpToEntryExtensions
    {
        public static Bundle.BundleEntryComponent ToBundleEntry(this HttpWebResponse response)
        {
            var result = new Bundle.BundleEntryComponent();

            result.TransactionResponse = new Bundle.BundleEntryTransactionResponseComponent();
            result.TransactionResponse.Status = response.StatusCode.ToString();

            var contentType = getContentType(response);
            var charEncoding = getCharacterEncoding(response);

            result.TransactionResponse.Location = response.Headers[HttpUtil.LOCATION] ?? response.Headers[HttpUtil.CONTENTLOCATION];

#if PORTABLE45
                    result.TransactionResponse.LastModified = response.Headers[HttpUtil.LASTMODIFIED];
#else
                    result.TransactionResponse.LastModified = response.LastModified;
#endif
            result.TransactionResponse.Etag = getETag(response);
            
            var body = readBody(response);

            if (body != null)
            {
                if (isBinaryResponse(response))
                    result.Resource = makeBinaryResource(body, contentType);
                else
                {
                    var bodyText = decodeBody(body, charEncoding);

                    bool success = false;
                    var resource = tryParseResource(bodyText, contentType, out success);

                    // tryParseResource will create an OperationOutcome if success == false
                    result.Resource = resource;
                    if (result.TransactionResponse.Location != null)
                        result.Resource.ResourceBase = new ResourceIdentity(result.TransactionResponse.Location).BaseUri;
                }
            }

            return result;
        }


        private static string getETag(HttpWebResponse response)
        {
            var result = response.Headers[HttpUtil.ETAG];

            if(result != null)
            {
                if(result.StartsWith(@"W\")) result = result.Substring(2);
                result = result.Trim('\"');
            }

            return result;
        }

        private static string getContentType(HttpWebResponse response)
        {
            if (!String.IsNullOrEmpty(response.ContentType))
            {
#if PORTABLE45
				return System.Net.Http.Headers.MediaTypeHeaderValue.Parse(response.ContentType).MediaType;
#else
                return new System.Net.Mime.ContentType(response.ContentType).MediaType;
#endif
            }
            else
                return null;
        }

        private static Encoding getCharacterEncoding(HttpWebResponse response)
        {
            Encoding result = null;

            if (!String.IsNullOrEmpty(response.ContentType))
            {
#if PORTABLE45
				var charset = System.Net.Http.Headers.MediaTypeHeaderValue.Parse(response.ContentType).CharSet;
#else
                var charset = new System.Net.Mime.ContentType(response.ContentType).CharSet;
#endif

                if (!String.IsNullOrEmpty(charset))
                    result = Encoding.GetEncoding(charset);
            }
            return result;
        }

        private static byte[] readBody(HttpWebResponse response)
        {
            if (response.ContentLength > 0)
            {
                long contentlength = response.ContentLength;
                return HttpUtil.ReadAllFromStream(response.GetResponseStream(), (int)contentlength);
            }
            else
                return null;
        }


        private static Resource tryParseResource(string bodyText, string contentType, out bool success)
        {           
            Resource result= null;

            var fhirType = ContentType.GetResourceFormatFromContentType(contentType);

            // Special case...this isn't even xml or json...probably some diagnostic text or html sent
            // by the server. Package in an OperationOutcome
            if (!FhirParser.ProbeIsJson(bodyText) && !FhirParser.ProbeIsXml(bodyText))
            {
                success = false;
                return OperationOutcome.ForMessage(bodyText);
            }

            try
            {
                if(fhirType == ResourceFormat.Json)
                    result = (Resource)FhirParser.ParseFromJson(bodyText);
                else
                    result = (Resource)FhirParser.ParseFromXml(bodyText);

                success = true;
                return result;
            }
            catch(FormatException exc)
            {
                success = false;
                return OperationOutcome.ForException(exc);
            }
        }


        private static bool isBinaryResponse(HttpWebResponse response)
        {
            var responseUri = response.ResponseUri.OriginalString;
            return responseUri.EndsWith("/Binary") || responseUri.EndsWith("/Binary?") || responseUri.Contains("/Binary/");
        }



        public static string decodeBody(byte[] body, Encoding enc)
        {
            if (body == null) return null;
            if (enc == null) enc = Encoding.UTF8;

            return (new StreamReader(new MemoryStream(body), enc, true)).ReadToEnd();
        }

        private static Binary makeBinaryResource(byte[] data, string contentType)
        {
            var binary = new Binary();

            binary.Content = data;
            binary.ContentType = contentType;

            return binary;
        }


        //public T BodyAsResource<T>() where T : Resource
        //{
        //    var result = BodyAsResource();

        //    if (!(result is T))
        //    {
        //        throw new FhirOperationException(
        //            String.Format("Received a resource of type {0} (FHIR: {1}), expected a {2} resource",
        //                            result.GetType().Name, result.TypeName, typeof(T).Name));
        //    }

        //    return (T)result;
        //}


      

        //public ResourceIdentity GetIdentityFromHeaders()
        //{
        //    var location = Location ?? ContentLocation;

        //    if (!String.IsNullOrEmpty(location))
        //    {
        //        ResourceIdentity reqId = new ResourceIdentity(location);

        //        if (reqId.VersionId == null && !String.IsNullOrEmpty(ETag))
        //        {
        //            Debug.WriteLine("Result did not have version nor location, using ETag instead");
        //            reqId = reqId.WithVersion(ETag);
        //        }

        //        return reqId;
        //    }

        //    return null;
        //}
    }
}
