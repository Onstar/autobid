﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace tobid.rest.json
{
    /// <summary>
    /// 根据TYPE字段内容创建相应Operation对象
    /// </summary>
    public class OperationConvert : JsonCreationConverter<Operation>
    {
        protected override Operation Create(Type objectType, JObject jObject)
        {
            JValue category = (JValue)this.GetType("type", jObject);
            String value = category.ToString();
            if ("BID".Equals(value))
                return new BidOperation();
            else if ("LOGIN".Equals(value))
                return new LoginOperation();
            else if ("STEP1".Equals(value))
                return new Step1Operation();
            else
                return null;
        }

        private Object GetType(String prop, JObject jObject)
        {
            return jObject[prop];
        }
    }

    public abstract class JsonCreationConverter<T> : Newtonsoft.Json.JsonConverter
    {
        protected abstract T Create(Type objectType, JObject jObject);

        public override bool CanConvert(Type objectType)
        {
            return typeof(T).IsAssignableFrom(objectType);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            JObject jObject = JObject.Load(reader);
            T target = Create(objectType, jObject);
            serializer.Populate(jObject.CreateReader(), target);
            return target;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }
    }
}
