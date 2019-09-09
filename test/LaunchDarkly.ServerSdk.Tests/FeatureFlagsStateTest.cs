﻿using System;
using System.Collections.Generic;
using System.Text;
using LaunchDarkly.Client;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace LaunchDarkly.Tests
{
    public class FeatureFlagsStateTest
    {
        [Fact]
        public void CanGetFlagValue()
        {
            var state = new FeatureFlagsState(true);
            var flag = new FeatureFlagBuilder("key").Build();
            state.AddFlag(flag, new JValue("value"), 1, null, false);

            Assert.Equal(ImmutableJsonValue.Of("value"), state.GetFlagValueJson("key"));
        }

        [Fact]
        public void CanGetDeprecatedFlagValue()
        {
            var state = new FeatureFlagsState(true);
            var flag = new FeatureFlagBuilder("key").Build();
            state.AddFlag(flag, new JValue("value"), 1, null, false);
#pragma warning disable 0618
            Assert.Equal(new JValue("value"), state.GetFlagValue("key"));
#pragma warning restore 0618
        }

        [Fact]
        public void UnknownFlagReturnsNullValue()
        {
            var state = new FeatureFlagsState(true);
#pragma warning disable 0618
            Assert.Null(state.GetFlagValue("key"));
#pragma warning restore 0618
            Assert.Equal(ImmutableJsonValue.Null, state.GetFlagValueJson("key"));
        }

        [Fact]
        public void CanGetFlagReason()
        {
            var state = new FeatureFlagsState(true);
            var flag = new FeatureFlagBuilder("key").Build();
            state.AddFlag(flag, new JValue("value"), 1, EvaluationReason.Fallthrough.Instance, false);

            Assert.Equal(EvaluationReason.Fallthrough.Instance, state.GetFlagReason("key"));
        }

        [Fact]
        public void UnknownFlagReturnsNullReason()
        {
            var state = new FeatureFlagsState(true);

            Assert.Null(state.GetFlagReason("key"));
        }

        [Fact]
        public void ReasonIsNullIfReasonsWereNotRecorded()
        {
            var state = new FeatureFlagsState(true);
            var flag = new FeatureFlagBuilder("key").Build();
            state.AddFlag(flag, new JValue("value"), 1, null, false);

            Assert.Null(state.GetFlagReason("key"));
        }

        [Fact]
        public void CanConvertToValuesMap()
        {
            var state = new FeatureFlagsState(true);
            var flag1 = new FeatureFlagBuilder("key1").Build();
            var flag2 = new FeatureFlagBuilder("key2").Build();
            state.AddFlag(flag1, new JValue("value1"), 0, null, false);
            state.AddFlag(flag2, new JValue("value2"), 1, null, false);

            var expected = new Dictionary<string, ImmutableJsonValue>
            {
                { "key1", ImmutableJsonValue.Of("value1") },
                { "key2", ImmutableJsonValue.Of("value2") }
            };
            Assert.Equal(expected, state.ToValuesJsonMap());
        }

        [Fact]
        public void CanConvertToDeprecatedValuesMap()
        {
            var state = new FeatureFlagsState(true);
            var flag1 = new FeatureFlagBuilder("key1").Build();
            var flag2 = new FeatureFlagBuilder("key2").Build();
            state.AddFlag(flag1, new JValue("value1"), 0, null, false);
            state.AddFlag(flag2, new JValue("value2"), 1, null, false);

            var expected = new Dictionary<string, JToken>
            {
                { "key1", new JValue("value1") },
                { "key2", new JValue("value2") }
            };
#pragma warning disable 0618
            Assert.Equal(expected, state.ToValuesMap());
#pragma warning restore 0618
        }

        [Fact]
        public void CanSerializeToJson()
        {
            var state = new FeatureFlagsState(true);
            var flag1 = new FeatureFlagBuilder("key1").Version(100).Build();
            var flag2 = new FeatureFlagBuilder("key2").Version(200)
                .TrackEvents(true).DebugEventsUntilDate(1000).Build();
            state.AddFlag(flag1, new JValue("value1"), 0, null, false);
            state.AddFlag(flag2, new JValue("value2"), 1, EvaluationReason.Fallthrough.Instance, false);

            var expectedString = @"{""key1"":""value1"",""key2"":""value2"",
                ""$flagsState"":{
                  ""key1"":{
                    ""variation"":0,""version"":100
                  },""key2"":{
                    ""variation"":1,""version"":200,""reason"":{""kind"":""FALLTHROUGH""},""trackEvents"":true,""debugEventsUntilDate"":1000
                  }
                },
                ""$valid"":true
            }";
            var expectedValue = JsonConvert.DeserializeObject<JToken>(expectedString);
            var actualString = JsonConvert.SerializeObject(state);
            var actualValue = JsonConvert.DeserializeObject<JToken>(actualString);
            TestUtils.AssertJsonEqual(expectedValue, actualValue);
        }

        [Fact]
        public void CanDeserializeFromJson()
        {
            var state = new FeatureFlagsState(true);
            var flag1 = new FeatureFlagBuilder("key1").Version(100).Build();
            var flag2 = new FeatureFlagBuilder("key2").Version(200)
                .TrackEvents(true).DebugEventsUntilDate(1000).Build();
            state.AddFlag(flag1, new JValue("value1"), 0, null, false);
            state.AddFlag(flag2, new JValue("value2"), 1, EvaluationReason.Fallthrough.Instance, false);

            var jsonString = JsonConvert.SerializeObject(state);
            var state1 = JsonConvert.DeserializeObject<FeatureFlagsState>(jsonString);

            Assert.Equal(state, state1);
        }
    }
}
