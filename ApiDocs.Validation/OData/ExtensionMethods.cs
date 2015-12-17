﻿/*
 * Markdown Scanner
 * Copyright (c) Microsoft Corporation
 * All rights reserved. 
 * 
 * MIT License
 * 
 * Permission is hereby granted, free of charge, to any person obtaining a copy of 
 * this software and associated documentation files (the ""Software""), to deal in 
 * the Software without restriction, including without limitation the rights to use, 
 * copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the
 * Software, and to permit persons to whom the Software is furnished to do so, 
 * subject to the following conditions:
 * 
 * The above copyright notice and this permission notice shall be included in all 
 * copies or substantial portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED *AS IS*, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, 
 * INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A 
 * PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT 
 * HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION 
 * OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE 
 * SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
 */

namespace ApiDocs.Validation.OData
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Xml.Linq;

    public static class ExtensionMethods
    {
        private static readonly Dictionary<string, SimpleDataType> ODataSimpleTypeMap = new Dictionary<string, SimpleDataType>()
        {
            { "Edm.String", SimpleDataType.String },
            { "Edm.Int64", SimpleDataType.Int64 },
            { "Edm.Int32", SimpleDataType.Int32 },
            { "Edm.Boolean", SimpleDataType.Boolean },
            { "Edm.DateTimeOffset", SimpleDataType.DateTimeOffset },
            { "Edm.Double", SimpleDataType.Double },
            { "Edm.Float", SimpleDataType.Float },
            { "Edm.Guid", SimpleDataType.Guid },
            { "Edm.TimeSpan", SimpleDataType.TimeSpan },
            { "Edm.Stream", SimpleDataType.Stream },
            { "Edm.Object", SimpleDataType.Object }
        };

        internal static bool ToBoolean(this string source)
        {
            if (string.IsNullOrEmpty(source)) return false;

            bool value;
            if (Boolean.TryParse(source, out value))
                return value;

            throw new ArgumentException(string.Format("Failed to convert {0} into a boolean", source));
        }

        internal static string AttributeValue(this XElement xml, XName attributeName)
        {
            var attribute = xml.Attribute(attributeName);
            if (null == attribute)
                return null;
            return attribute.Value;
        }

        /// <summary>
        /// Resovles a fully qualified type identifier within a collection of schema.
        /// </summary>
        /// <param name="schemas"></param>
        /// <param name="identifier"></param>
        /// <returns></returns>
        internal static ComplexType FindTypeWithIdentifier(this IEnumerable<Schema> schemas, string identifier)
        {
            // onedrive.item
            int splitIndex = identifier.LastIndexOf('.');
            if (splitIndex == -1)
                throw new ArgumentException("identifier should be of format {schema}.{type}");

            string schemaName = identifier.Substring(0, splitIndex);
            string typeName = identifier.Substring(splitIndex + 1);

            // Resolve the schema first
            var schema = (from s in schemas
                          where s.Namespace == schemaName
                          select s).FirstOrDefault();
            if (null == schema)
            {
                return null;
            }

            // Look for a matching complex type
            var matchingComplexType = (from ct in schema.ComplexTypes
                                       where ct.Name == typeName
                                       select ct).FirstOrDefault();
            if (null != matchingComplexType)
            {
                return matchingComplexType;
            }

            // Look for a matching entity type
            var matchingEntityType = (from et in schema.Entities
                                      where et.Name == typeName
                                      select et).FirstOrDefault();
            return matchingEntityType;
        }

        internal static ComplexType FindTypeWithIdentifier(this EntityFramework edmx, string identifier)
        {
            var foundType = edmx.DataServices.Schemas.FindTypeWithIdentifier(identifier);
            if (foundType != null)
                return foundType;

            throw new KeyNotFoundException("Couldn't resolve type identifier: " + identifier);
        }

        internal static IODataNavigable LookupNavigableType(this EntityFramework edmx, string identifier)
        {
            var foundType = edmx.DataServices.Schemas.FindTypeWithIdentifier(identifier);
            if (null != foundType)
            {
                return (IODataNavigable)foundType;
            }

            SimpleDataType simpleType = identifier.ToODataSimpleType();
            if (simpleType != SimpleDataType.Object)
                return new ODataSimpleType(simpleType);

            throw new InvalidOperationException("Could not resolve type identifier: " + identifier);
        }

        /// <summary>
        /// Resolve an IODataNavigable instance into a type identifier string.
        /// </summary>
        /// <param name="edmx"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        public static string LookupIdentifierForType(this EntityFramework edmx, IODataNavigable type)
        {
            if (type is ODataSimpleType)
            {
                return ((ODataSimpleType)type).Type.ODataResourceName();
            }

            foreach (var schema in edmx.DataServices.Schemas)
            {
                if (type is EntityType)
                {
                    foreach (var et in schema.Entities)
                    {
                        if (et == type)
                            return schema.Namespace + "." + et.Name;
                    }
                }
                else if (type is ComplexType)
                {
                    foreach (var ct in schema.ComplexTypes)
                    {
                        if (ct == type)
                            return schema.Namespace + "." + ct.Name;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Returns "oneDrive" for "oneDrive.item" input.
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public static string NamespaceOnly(this string type)
        {
            var trimPoint = type.LastIndexOf('.');
            if (trimPoint >= 0)
                return type.Substring(0, trimPoint);

            throw new InvalidOperationException("Type doesn't appear to have a namespace assocaited with it: " + type);
        }


        /// <summary>
        /// Returns "item" for "oneDrive.item" input.
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public static string TypeOnly(this string type)
        {
            var trimPoint = type.LastIndexOf('.');
            return type.Substring(trimPoint + 1);
        }

        /// <summary>
        /// Convert a ParameterDataType instance into the OData equivelent.
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public static string ODataResourceName(this ParameterDataType type)
        {

            if (type.Type == SimpleDataType.Object && !string.IsNullOrEmpty(type.CustomTypeName))
            {
                return type.CustomTypeName;
            }

            if (type.Type == SimpleDataType.Collection)
            {
                return string.Format(
                    "Collection({0})",
                    type.CollectionResourceType.ODataResourceName(type.CustomTypeName));
            }

            return type.Type.ODataResourceName();
        }




        /// <summary>
        /// Convert a simple type into OData equivelent. If Object is specified, a customDataType can be returned instead.
        /// </summary>
        /// <param name="type"></param>
        /// <param name="customDataType"></param>
        /// <returns></returns>
        public static string ODataResourceName(this SimpleDataType type, string customDataType = null)
        {
            if (type == SimpleDataType.Object && !string.IsNullOrEmpty(customDataType))
                return customDataType;

            string typeName = (from kv in ODataSimpleTypeMap where kv.Value == type select kv.Key).SingleOrDefault();
            if (null == typeName)
            {
                throw new NotSupportedException(string.Format("Attempted to convert an unsupported SimpleDataType into OData: {0}", type));
            }
            return typeName;
        }

        /// <summary>
        /// Convert from a typeName like Edm.String back into the internal SimpleDataType enum.
        /// </summary>
        /// <param name="typeName"></param>
        /// <returns></returns>
        public static SimpleDataType ToODataSimpleType(this string typeName)
        {
            SimpleDataType? dataType =
                (from kv in ODataSimpleTypeMap where kv.Key == typeName select kv.Value).SingleOrDefault();

            if (dataType.HasValue)
                return dataType.Value;

            return SimpleDataType.Object;
        }
    }
}
