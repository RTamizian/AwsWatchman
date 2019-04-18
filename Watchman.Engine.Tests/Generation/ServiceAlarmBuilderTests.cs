using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Moq;
using NUnit.Framework;
using Watchman.AwsResources;
using Watchman.Configuration;
using Watchman.Configuration.Generic;
using Watchman.Engine.Generation;

namespace Watchman.Engine.Tests.Generation
{
    [TestFixture]
    public class ServiceAlarmBuilderTests
    {
        public class FakeResource { }

        private Mock<IResourceSource<FakeResource>> _fakeTableSource;
        private Mock<IAlarmDimensionProvider<FakeResource>> _fakeDimensionProvider;
        private Mock<IResourceAttributesProvider<FakeResource, ResourceConfig>> _fakeAttributeProvider;

        private ResourceAlarmGenerator<FakeResource, ResourceConfig> CreateSut(IEnumerable<AlarmDefinition> defaults)
        {
            return new ResourceAlarmGenerator<FakeResource, ResourceConfig>(
                _fakeTableSource.Object,
                _fakeDimensionProvider.Object,
                AlarmDefaults<FakeResource>.FromDefaults(defaults),
                _fakeAttributeProvider.Object);
        }

        [SetUp]
        public void SetUp()
        {
            _fakeTableSource = new Mock<IResourceSource<FakeResource>>();
            _fakeDimensionProvider = new Mock<IAlarmDimensionProvider<FakeResource>>();
            _fakeAttributeProvider = new Mock<IResourceAttributesProvider<FakeResource, ResourceConfig>>();
        }

        private void SetupFakeResources(IList<string> resourceNames)
        {
            _fakeTableSource.Setup(x => x.GetResourceNamesAsync())
                   .ReturnsAsync(resourceNames);

            foreach (var resource in resourceNames)
            {
                _fakeTableSource.Setup(x => x.GetResourceAsync(resource))
                    .ReturnsAsync(new AwsResource<FakeResource>(resource, new FakeResource()));
            }
        }

        [Test]
        public async Task DefaultThresholdIsUsedWhenThereAreNoOverrides()
        {
            // arrange
            var defaults = DefineOneAlarm();

            var alertingGroup = new ServiceAlertingGroup<ResourceConfig>
            {
                GroupParameters = new AlertingGroupParameters("TestAlarm", "Suffix"),
                Service = new AwsServiceAlarms<ResourceConfig>
                {
                    Resources = new List<ResourceThresholds<ResourceConfig>>
                    {
                        new ResourceThresholds<ResourceConfig>
                        {
                            Name = "ResourceA"
                        },
                         new ResourceThresholds<ResourceConfig>
                        {
                            Name = "ResourceB"
                        }
                    }
                }
            };

            SetupFakeResources(new[] { "ResourceA", "ResourceB" });

            // act

            var result = await CreateSut(defaults).GenerateAlarmsFor(alertingGroup.Service,
                alertingGroup.GroupParameters);

            // assert

            var resourceAlarmA = result.FirstOrDefault(x => x.Resource.Name == "ResourceA");
            var resourceAlarmB = result.FirstOrDefault(x => x.Resource.Name == "ResourceB");

            Assert.That(resourceAlarmA, Is.Not.Null);
            Assert.That(resourceAlarmA.AlarmDefinition.Threshold.Value, Is.EqualTo(400));

            Assert.That(resourceAlarmB, Is.Not.Null);
            Assert.That(resourceAlarmB.AlarmDefinition.Threshold.Value, Is.EqualTo(400));
        }

        [Test]
        public async Task ResourceThresholdsTakePrecedenceOverDefaults()
        {
            // arrange
            var defaults = DefineOneAlarm();

            var alertingGroup = new ServiceAlertingGroup<ResourceConfig>
            {
                GroupParameters = new AlertingGroupParameters("TestAlarm", "Suffix"),
                Service = new AwsServiceAlarms<ResourceConfig>
                {
                    Resources = new List<ResourceThresholds<ResourceConfig>>
                    {
                        new ResourceThresholds<ResourceConfig>
                        {
                            Name = "ResourceA",
                            Values = new Dictionary<string, AlarmValues>
                            {
                                {"AlarmName", 200}
                            }
                        },
                         new ResourceThresholds<ResourceConfig>
                        {
                            Name = "ResourceB"
                        }
                    }
                }
            };

            SetupFakeResources(new[] {"ResourceA", "ResourceB"});

            // act

            var result = await CreateSut(defaults).GenerateAlarmsFor(alertingGroup.Service,
                alertingGroup.GroupParameters);

            // assert

            var resourceAlarmA = result.FirstOrDefault(x => x.Resource.Name == "ResourceA");
            var resourceAlarmB = result.FirstOrDefault(x => x.Resource.Name == "ResourceB");

            Assert.That(resourceAlarmA, Is.Not.Null);
            Assert.That(resourceAlarmA.AlarmDefinition.Threshold.Value, Is.EqualTo(200));

            Assert.That(resourceAlarmB, Is.Not.Null);
            Assert.That(resourceAlarmB.AlarmDefinition.Threshold.Value, Is.EqualTo(400));
        }

        [Test]
        public async Task ResourceAndGroupThresholdsTakePrecedenceOverDefault()
        {
            // arrange
            var defaults = DefineOneAlarm();

            var alertingGroup = new ServiceAlertingGroup<ResourceConfig>
            {
                GroupParameters = new AlertingGroupParameters("TestAlarm", "Suffix"),
                Service = new AwsServiceAlarms<ResourceConfig>
                {
                    Resources = new List<ResourceThresholds<ResourceConfig>>
                    {
                        new ResourceThresholds<ResourceConfig>
                        {
                            Name = "ResourceA",
                            Values = new Dictionary<string, AlarmValues>
                            {
                                {"AlarmName", 200}
                            }
                        },
                         new ResourceThresholds<ResourceConfig>
                        {
                            Name = "ResourceB"
                        }
                    },
                    Values = new Dictionary<string, AlarmValues>
                    {
                        { "AlarmName", 300 }
                    }
                }
            };

            SetupFakeResources(new[] { "ResourceA", "ResourceB" });

            // act

            var result = await CreateSut(defaults).GenerateAlarmsFor(alertingGroup.Service,
                alertingGroup.GroupParameters);

            // assert

            var resourceAlarmA = result.FirstOrDefault(x => x.Resource.Name == "ResourceA");
            var resourceAlarmB = result.FirstOrDefault(x => x.Resource.Name == "ResourceB");

            Assert.That(resourceAlarmA, Is.Not.Null);
            Assert.That(resourceAlarmA.AlarmDefinition.Threshold.Value, Is.EqualTo(200));
            Assert.That(resourceAlarmB, Is.Not.Null);
            Assert.That(resourceAlarmB.AlarmDefinition.Threshold.Value, Is.EqualTo(300));
        }

        [Test]
        public async Task DefaultEvaluationPeriodsIsUsedWhenThereAreNoOverrides()
        {
            // arrange
            var defaults = DefineOneAlarm();

            var alertingGroup = new ServiceAlertingGroup<ResourceConfig>
            {
                GroupParameters = new AlertingGroupParameters("TestAlarm", "Suffix"),
                Service = new AwsServiceAlarms<ResourceConfig>
                {
                    Resources = new List<ResourceThresholds<ResourceConfig>>
                    {
                        new ResourceThresholds<ResourceConfig>
                        {
                            Name = "ResourceA"
                        },
                         new ResourceThresholds<ResourceConfig>
                        {
                            Name = "ResourceB"
                        }
                    }
                }
            };

            SetupFakeResources(new[] { "ResourceA", "ResourceB" });

            // act

            var result = await CreateSut(defaults).GenerateAlarmsFor(alertingGroup.Service,
                alertingGroup.GroupParameters);

            // assert

            var resourceAlarmA = result.FirstOrDefault(x => x.Resource.Name == "ResourceA");
            var resourceAlarmB = result.FirstOrDefault(x => x.Resource.Name == "ResourceB");

            Assert.That(resourceAlarmA, Is.Not.Null);
            Assert.That(resourceAlarmA.AlarmDefinition.EvaluationPeriods, Is.EqualTo(2));

            Assert.That(resourceAlarmB, Is.Not.Null);
            Assert.That(resourceAlarmB.AlarmDefinition.EvaluationPeriods, Is.EqualTo(2));
        }

        [Test]
        public async Task EvaluationPeriodsAreSelectedFromResourceAndGroup()
        {
            // arrange
            var defaults = DefineOneAlarm();

            var alertingGroup = new ServiceAlertingGroup<ResourceConfig>
            {
                GroupParameters = new AlertingGroupParameters("TestAlarm", "Suffix"),
                Service = new AwsServiceAlarms<ResourceConfig>
                {
                    Resources = new List<ResourceThresholds<ResourceConfig>>
                    {
                        new ResourceThresholds<ResourceConfig>
                        {
                            Name = "ResourceA",
                            Values = new Dictionary<string, AlarmValues>
                            {
                                {"AlarmName", new AlarmValues(200, 3)}
                            }
                        },
                         new ResourceThresholds<ResourceConfig>
                        {
                            Name = "ResourceB"
                        }
                    },
                    Values = new Dictionary<string, AlarmValues>
                    {
                        { "AlarmName", new AlarmValues(300, 4) }
                    }
                }
            };

            SetupFakeResources(new[] { "ResourceA", "ResourceB" });

            // act

            var result = await CreateSut(defaults).GenerateAlarmsFor(alertingGroup.Service,
                alertingGroup.GroupParameters);

            // assert

            var resourceAlarmA = result.FirstOrDefault(x => x.Resource.Name == "ResourceA");
            var resourceAlarmB = result.FirstOrDefault(x => x.Resource.Name == "ResourceB");

            Assert.That(resourceAlarmA, Is.Not.Null);
            Assert.That(resourceAlarmA.AlarmDefinition.EvaluationPeriods, Is.EqualTo(3));
            Assert.That(resourceAlarmB, Is.Not.Null);
            Assert.That(resourceAlarmB.AlarmDefinition.EvaluationPeriods, Is.EqualTo(4));
        }

        [Test]
        public async Task DefaultPeriodLengthIsUsedWhenThereAreNoOverrides()
        {
            // arrange
            var defaults = DefineOneAlarm();

            var alertingGroup = new ServiceAlertingGroup<ResourceConfig>
            {
                GroupParameters = new AlertingGroupParameters("TestAlarm", "Suffix"),
                Service = new AwsServiceAlarms<ResourceConfig>
                {
                    Resources = new List<ResourceThresholds<ResourceConfig>>
                    {
                        new ResourceThresholds<ResourceConfig>
                        {
                            Name = "ResourceA"
                        },
                         new ResourceThresholds<ResourceConfig>
                        {
                            Name = "ResourceB"
                        }
                    }
                }
            };

            SetupFakeResources(new[] { "ResourceA", "ResourceB" });

            // act

            var result = await CreateSut(defaults).GenerateAlarmsFor(alertingGroup.Service,
                alertingGroup.GroupParameters);

            // assert

            var resourceAlarmA = result.FirstOrDefault(x => x.Resource.Name == "ResourceA");
            var resourceAlarmB = result.FirstOrDefault(x => x.Resource.Name == "ResourceB");

            Assert.That(resourceAlarmA, Is.Not.Null);
            Assert.That(resourceAlarmA.AlarmDefinition.Period, Is.EqualTo(TimeSpan.FromMinutes(1)));

            Assert.That(resourceAlarmB, Is.Not.Null);
            Assert.That(resourceAlarmB.AlarmDefinition.Period, Is.EqualTo(TimeSpan.FromMinutes(1)));
        }

        [Test]
        public async Task PeriodLengthsAreSelectedFromResourceAndGroup()
        {
            // arrange
            var defaults = DefineOneAlarm();

            var alertingGroup = new ServiceAlertingGroup<ResourceConfig>
            {
                GroupParameters = new AlertingGroupParameters("TestAlarm", "Suffix"),
                Service = new AwsServiceAlarms<ResourceConfig>
                {
                    Resources = new List<ResourceThresholds<ResourceConfig>>
                    {
                        new ResourceThresholds<ResourceConfig>
                        {
                            Name = "ResourceA",
                            Values = new Dictionary<string, AlarmValues>
                            {
                                {"AlarmName", new AlarmValues(200, periodMinutes:10)}
                            }
                        },
                         new ResourceThresholds<ResourceConfig>
                        {
                            Name = "ResourceB"
                        }
                    },
                    Values = new Dictionary<string, AlarmValues>
                    {
                        { "AlarmName", new AlarmValues(300, periodMinutes:7) }
                    }
                }
            };

            SetupFakeResources(new[] { "ResourceA", "ResourceB" });

            // act

            var result = await CreateSut(defaults).GenerateAlarmsFor(alertingGroup.Service,
                alertingGroup.GroupParameters);

            // assert

            var resourceAlarmA = result.FirstOrDefault(x => x.Resource.Name == "ResourceA");
            var resourceAlarmB = result.FirstOrDefault(x => x.Resource.Name == "ResourceB");

            Assert.That(resourceAlarmA, Is.Not.Null);
            Assert.That(resourceAlarmA.AlarmDefinition.Period, Is.EqualTo(TimeSpan.FromMinutes(10)));
            Assert.That(resourceAlarmB, Is.Not.Null);
            Assert.That(resourceAlarmB.AlarmDefinition.Period, Is.EqualTo(TimeSpan.FromMinutes(7)));
        }


        private static List<AlarmDefinition> DefineOneAlarm()
        {
            return new List<AlarmDefinition>
            {
                new AlarmDefinition
                {
                    Name = "AlarmName",
                    EvaluationPeriods = 2,
                    Period = TimeSpan.FromMinutes(1),
                    Threshold = new Threshold
                    {
                        ThresholdType = ThresholdType.Absolute,
                        Value = 400
                    }
                }
            };
        }

        [Test]
        public async Task ResourceThresholdCanOverrideServiceValueWithoutResettingEvaluationPeriods()
        {
            // arrange
            var defaults = new List<AlarmDefinition>
            {
                new AlarmDefinition
                {
                    Name = "AlarmName",
                    EvaluationPeriods = 2,
                    Threshold = new Threshold
                    {
                        ThresholdType = ThresholdType.Absolute,
                        Value = 400
                    }

                }
            };

            var alertingGroup = new ServiceAlertingGroup<ResourceConfig>
            {
                GroupParameters = new AlertingGroupParameters("TestAlarm", "Suffix"),
                Service = new AwsServiceAlarms<ResourceConfig>
                {
                    Resources = new List<ResourceThresholds<ResourceConfig>>
                    {
                        new ResourceThresholds<ResourceConfig>
                        {
                            Name = "ResourceA",
                            Values = new Dictionary<string, AlarmValues>
                            {
                                {"AlarmName", new AlarmValues(200) }
                            }
                        }
                    },
                    Values = new Dictionary<string, AlarmValues>
                    {
                        { "AlarmName", new AlarmValues(100, 5) }
                    }
                }
            };

            SetupFakeResources(new[] { "ResourceA" });

            // act

            var result = await CreateSut(defaults).GenerateAlarmsFor(alertingGroup.Service,
                alertingGroup.GroupParameters);

            // assert

            var resourceAlarmA = result.FirstOrDefault(x => x.Resource.Name == "ResourceA");

            Assert.That(resourceAlarmA, Is.Not.Null);
            Assert.That(resourceAlarmA.AlarmDefinition.EvaluationPeriods, Is.EqualTo(5));
            Assert.That(resourceAlarmA.AlarmDefinition.Threshold.Value, Is.EqualTo(200));
        }

        [Test]
        public async Task ResourceThresholdCanOverrideServiceValueWithoutResettingEvaluationPeriodsFromDefaults()
        {
            // arrange
            var defaults = new List<AlarmDefinition>
            {
                new AlarmDefinition
                {
                    Name = "AlarmName",
                    EvaluationPeriods = 2,
                    Threshold = new Threshold
                    {
                        ThresholdType = ThresholdType.Absolute,
                        Value = 400
                    }

                }
            };

            var alertingGroup = new ServiceAlertingGroup<ResourceConfig>
            {
                GroupParameters = new AlertingGroupParameters("TestAlarm", "Suffix"),
                Service = new AwsServiceAlarms<ResourceConfig>
                {
                    Resources = new List<ResourceThresholds<ResourceConfig>>
                    {
                        new ResourceThresholds<ResourceConfig>
                        {
                            Name = "ResourceA",
                            Values = new Dictionary<string, AlarmValues>
                            {
                                {"AlarmName", new AlarmValues(200) }
                            }
                        }
                    }
                }
            };

            SetupFakeResources(new[] { "ResourceA" });

            // act

            var result = await CreateSut(defaults).GenerateAlarmsFor(alertingGroup.Service,
                alertingGroup.GroupParameters);

            // assert

            var resourceAlarmA = result.FirstOrDefault(x => x.Resource.Name == "ResourceA");

            Assert.That(resourceAlarmA, Is.Not.Null);
            Assert.That(resourceAlarmA.AlarmDefinition.EvaluationPeriods, Is.EqualTo(2));
            Assert.That(resourceAlarmA.AlarmDefinition.Threshold.Value, Is.EqualTo(200));
        }

        [Test]
        public async Task ResourceEvaluationPeriodsCanOverrideServiceValueWithoutResettingThreshold()
        {
            // arrange
            var defaults = new List<AlarmDefinition>
            {
                new AlarmDefinition
                {
                    Name = "AlarmName",
                    EvaluationPeriods = 2,
                    Threshold = new Threshold
                    {
                        ThresholdType = ThresholdType.Absolute,
                        Value = 400
                    }

                }
            };

            var alertingGroup = new ServiceAlertingGroup<ResourceConfig>
            {
                GroupParameters = new AlertingGroupParameters("TestAlarm", "Suffix"),
                Service = new AwsServiceAlarms<ResourceConfig>
                {
                    Resources = new List<ResourceThresholds<ResourceConfig>>
                    {
                        new ResourceThresholds<ResourceConfig>
                        {
                            Name = "ResourceA",
                            Values = new Dictionary<string, AlarmValues>
                            {
                                {"AlarmName", new AlarmValues(null, 17) }
                            }
                        }
                    },
                    Values = new Dictionary<string, AlarmValues>
                    {
                        { "AlarmName", new AlarmValues(125, 5) }
                    }
                }
            };

            SetupFakeResources(new[] { "ResourceA" });

            // act

            var result = await CreateSut(defaults).GenerateAlarmsFor(alertingGroup.Service,
                alertingGroup.GroupParameters);

            // assert

            var resourceAlarmA = result.FirstOrDefault(x => x.Resource.Name == "ResourceA");

            Assert.That(resourceAlarmA, Is.Not.Null);
            Assert.That(resourceAlarmA.AlarmDefinition.EvaluationPeriods, Is.EqualTo(17));
            Assert.That(resourceAlarmA.AlarmDefinition.Threshold.Value, Is.EqualTo(125));
        }
    }
}
