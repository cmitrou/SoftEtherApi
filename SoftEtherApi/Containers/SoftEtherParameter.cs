﻿using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace SoftEtherApi.Containers
{
    public class SoftEtherParameter
    {
        public string Key { get; }
        public string ValueType { get; }
        public List<object> Value { get; private set; } = new List<object>();
        public bool ValueIsArray { get; private set; } = false;

        public SoftEtherParameter(string key, string valueType, object value)
        {
            Key = key;
            ValueType = valueType;

            if (value is IEnumerable valueIter)
            {
                Value.AddRange(valueIter.Cast<object>());
                ValueIsArray = true;
            }
            else
                Value.Add(value);
        }
        
        public SoftEtherParameter(string key, string valueType, string value)
        {
            Key = key;
            ValueType = valueType;
            
            Value.Add(value);
        }
        
        public SoftEtherParameter(string key, string valueType, byte[] value)
        {
            Key = key;
            ValueType = valueType;
            
            Value.Add(value);
        }


        public bool ValueIsNull()
        {
            return Value == null;
        }

        public void RemoveNullFromValueArray()
        {
            if (ValueIsNull() || !ValueIsArray)
                return;
            
            Value = Value.Where(m => m != null).ToList();
        }
    }
}