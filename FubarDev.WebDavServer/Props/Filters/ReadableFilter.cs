﻿// <copyright file="ReadableFilter.cs" company="Fubar Development Junker">
// Copyright (c) Fubar Development Junker. All rights reserved.
// </copyright>

using System.Collections.Generic;
using System.Linq;

namespace FubarDev.WebDavServer.Props.Filters
{
    public class ReadableFilter : IPropertyFilter
    {
        public void Reset()
        {
        }

        public bool IsAllowed(IProperty property)
        {
            return property is IUntypedReadableProperty;
        }

        public void NotifyOfSelection(IProperty property)
        {
        }

        public IEnumerable<MissingProperty> GetMissingProperties()
        {
            return Enumerable.Empty<MissingProperty>();
        }
    }
}