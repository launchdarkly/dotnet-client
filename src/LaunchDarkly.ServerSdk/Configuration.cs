﻿using System;
using LaunchDarkly.Sdk.Interfaces;
using LaunchDarkly.Sdk.Server.Interfaces;

namespace LaunchDarkly.Sdk.Server
{
    /// <summary>
    /// Configuration options for <see cref="LdClient"/>. This class should normally be constructed with
    /// <see cref="Configuration.Builder(string)"/>.
    /// </summary>
    /// <remarks>
    /// Instances of <see cref="Configuration"/> are immutable once created. They can be created with the factory method
    /// <see cref="Configuration.Default(string)"/>, or using a builder pattern with <see cref="Configuration.Builder(string)"/>
    /// or <see cref="Configuration.Builder(Configuration)"/>.
    /// </remarks>
    public class Configuration
    {
        private readonly IDataSourceFactory _dataSourceFactory;
        private readonly IDataStoreFactory _dataStoreFactory;
        private readonly bool _diagnosticOptOut;
        private readonly IEventProcessorFactory _eventProcessorFactory;
        private readonly IHttpConfigurationFactory _httpConfigurationFactory;
        private readonly ILoggingConfigurationFactory _loggingConfigurationFactory;
        private readonly bool _offline;
        private readonly string _sdkKey;
        private readonly TimeSpan _startWaitTime;

        /// <summary>
        /// The SDK key for your LaunchDarkly environment.
        /// </summary>
        public string SdkKey => _sdkKey;

        /// <summary>
        /// How long the client constructor will block awaiting a successful connection to
        /// LaunchDarkly.
        /// </summary>
        /// <remarks>
        /// Setting this to 0 will not block and will cause the constructor to return immediately. The
        /// default value is 10 seconds.
        /// </remarks>
        public TimeSpan StartWaitTime => _startWaitTime;

        /// <summary>
        /// Whether or not this client is offline. If true, no calls to Launchdarkly will be made.
        /// </summary>
        public bool Offline => _offline;

        /// <summary>
        /// A factory object that creates an implementation of <see cref="IDataStore"/>, to be used
        /// for holding feature flags and related data received from LaunchDarkly.
        /// </summary>
        /// <remarks>
        /// The default is <see cref="Components.InMemoryDataStore"/>, but you may provide a custom
        /// implementation.
        /// </remarks>
        public IDataStoreFactory DataStoreFactory => _dataStoreFactory;

        /// <summary>
        /// A factory object that creates an implementation of <see cref="IEventProcessor"/>, which will
        /// process all analytics events.
        /// </summary>
        /// <remarks>
        /// The default is <see cref="Components.SendEvents"/>, but you may provide a custom
        /// implementation.
        /// </remarks>
        public IEventProcessorFactory EventProcessorFactory => _eventProcessorFactory;

        /// <summary>
        /// A factory object that creates an implementation of <see cref="IDataSource"/>, which will
        /// receive feature flag data.
        /// </summary>
        public IDataSourceFactory DataSourceFactory => _dataSourceFactory;

        /// <summary>
        /// A factory object that creates an implementation of <see cref="IHttpConfiguration"/>, defining the
        /// SDK's networking behavior.
        /// </summary>
        public IHttpConfigurationFactory HttpConfigurationFactory => _httpConfigurationFactory;

        /// <summary>
        /// A factory object that creates an implementation of <see cref="ILoggingConfiguration"/>, defining
        /// the SDK's logging configuration.
        /// </summary>
        /// <remarks>
        /// SDK components should not use this property directly; instead, the SDK client will use it to create a
        /// logger instance which will be in <see cref="LdClientContext"/>.
        /// </remarks>
        public ILoggingConfigurationFactory LoggingConfigurationFactory => _loggingConfigurationFactory;

        /// <summary>
        /// A string that will be sent to LaunchDarkly to identify the SDK type.
        /// </summary>
        public string UserAgentType { get { return "DotNetClient"; } }

        /// <summary>
        /// True if diagnostic events have been disabled.
        /// </summary>
        public bool DiagnosticOptOut => _diagnosticOptOut;

        internal static readonly TimeSpan DefaultStartWaitTime = TimeSpan.FromSeconds(10);

        /// <summary>
        /// Creates a configuration with all parameters set to the default.
        /// </summary>
        /// <param name="sdkKey">the SDK key for your LaunchDarkly environment</param>
        /// <returns>a <c>Configuration</c> instance</returns>
        public static Configuration Default(string sdkKey)
        {
            return new ConfigurationBuilder(sdkKey).Build();
        }

        /// <summary>
        /// Creates an <see cref="IConfigurationBuilder"/> for constructing a configuration object using a fluent syntax.
        /// </summary>
        /// <remarks>
        /// This is the only method for building a <see cref="Configuration"/> if you are setting properties
        /// besides the <c>SdkKey</c>. The <see cref="IConfigurationBuilder"/> has methods for setting any number of
        /// properties, after which you call <see cref="IConfigurationBuilder.Build"/> to get the resulting
        /// <c>Configuration</c> instance.
        /// </remarks>
        /// <example>
        /// <code>
        ///     var config = Configuration.Builder("my-sdk-key")
        ///         .StartWaitTime(TimeSpan.FromSeconds(5))
        ///         .Build();
        /// </code>
        /// </example>
        /// <param name="sdkKey">the SDK key for your LaunchDarkly environment</param>
        /// <returns>a builder object</returns>
        public static IConfigurationBuilder Builder(string sdkKey)
        {
            return new ConfigurationBuilder(sdkKey);
        }

        /// <summary>
        /// Creates an <see cref="IConfigurationBuilder"/> based on an existing configuration.
        /// </summary>
        /// <remarks>
        /// Modifying properties of the builder will not affect the original configuration object.
        /// </remarks>
        /// <example>
        /// <code>
        ///     var configWithCustomEventProperties = Configuration.Builder(originalConfig)
        ///         .Events(Components.SendEvents().Capacity(50000))
        ///         .Build();
        /// </code>
        /// </example>
        /// <param name="fromConfiguration">the existing configuration</param>
        /// <returns>a builder object</returns>
        public static IConfigurationBuilder Builder(Configuration fromConfiguration)
        {
            return new ConfigurationBuilder(fromConfiguration);
        }

        internal Configuration(ConfigurationBuilder builder)
        {
            _dataSourceFactory = builder._dataSourceFactory;
            _dataStoreFactory = builder._dataStoreFactory;
            _diagnosticOptOut = builder._diagnosticOptOut;
            _eventProcessorFactory = builder._eventProcessorFactory;
            _httpConfigurationFactory = builder._httpConfigurationFactory;
            _loggingConfigurationFactory = builder._loggingConfigurationFactory;
            _offline = builder._offline;
            _sdkKey = builder._sdkKey;
            _startWaitTime = builder._startWaitTime;
        }
    }
}
