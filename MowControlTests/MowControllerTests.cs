using Microsoft.VisualStudio.TestTools.UnitTesting;
using MowControl;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MowerTests
{
    [TestClass]
    public class MowControllerTests
    {
        private static void RunOverTime(MowController mowController, TestSystemTime systemTime, int hours, int minutes)
        {
            minutes = hours * 60 + minutes;

            for (int i = 0; i < minutes * 2; i++)
            {
                mowController.CheckAndAct();
                systemTime.TickSeconds(30);
            }
        }

        [TestMethod]
        public void CheckAndAct_ConfigOk_NoException()
        {
            // Arrange
            var timeIntervals = new List<TimeInterval>()
            {
                new TimeInterval(0, 0, 10, 0)
            };

            var config = new MowControlConfig()
            {
                TimeIntervals = timeIntervals,
                AverageWorkPerDayHours = 12,
                MaxHourlyThunderPercent = 0,
                MaxHourlyPrecipitaionMillimeter = 0
            };
            var powerSwitch = new TestPowerSwitch();
            var systemTime = new TestSystemTime(DateTime.Now);
            var homeSensor = new TestHomeSensor(systemTime, true);
            var logger = new MowLogger();
            var weatherForecast = new TestWeatherForecast(true, systemTime);
            var rainSensor = new TestRainSensor(isWet: false);
            var mowController = new MowController(config, powerSwitch, weatherForecast, systemTime, homeSensor, logger, rainSensor);
            Exception receivedException = null;

            // Act
            try
            {
                mowController.CheckAndAct();
            }
            catch (Exception ex)
            {
                receivedException = ex;
            }

            // Assert
            Assert.IsNull(receivedException);
        }

        [TestMethod]
        public void CheckAndAct_PeriodNull_InvalidOperationException()
        {
            // Arrange
            var systemTime = new SystemTime();
            var config = new MowControlConfig()
            {
                TimeIntervals = null
            };
            var powerSwitch = new TestPowerSwitch();
            var homeSensor = new TestHomeSensor(systemTime, true);
            var rainSensor = new TestRainSensor(isWet: false);
            var mowController = new MowController(config, powerSwitch, null, systemTime, homeSensor, null, rainSensor);
            Exception receivedException = null;

            // Act
            try
            {
                mowController.CheckAndAct();
            }
            catch (Exception ex)
            {
                receivedException = ex;
            }

            // Assert
            Assert.IsNotNull(receivedException);
            Assert.IsInstanceOfType(receivedException, typeof(InvalidOperationException));
        }

        [TestMethod]
        public void CheckAndAct_GoodWeatherAndCloseToStartMowing_CurrentIsTurnedOn()
        {
            // Arrange
            var systemTime = new TestSystemTime(2018, 7, 24, 6, 0);
            var config = TestFactory.NewConfig6To12();
            var powerSwitch = new TestPowerSwitch();
            var weatherForecast = TestFactory.NewWeatherForecastGood(systemTime);
            var homeSensor = new TestHomeSensor(systemTime, true);
            var logger = TestFactory.NewMowLogger(systemTime.Now);
            var rainSensor = new TestRainSensor(isWet: false);
            var mowController = new MowController(config, powerSwitch, weatherForecast, systemTime, homeSensor, logger, rainSensor);

            // Act
            mowController.CheckAndAct();

            // Assert
            Assert.AreEqual(powerSwitch.Status, PowerStatus.On);
            Assert.IsTrue(powerSwitch.HasBeenTurnedOnOnce);

            var logItems = logger.LogItems.Where(x => x.Type == LogType.PowerOn).ToList();

            Assert.AreEqual(1, logItems.Count);
            Assert.AreEqual(LogType.PowerOn, logItems[0].Type);
            string expectedLogDate = "2018-07-24 06:00";
            Assert.AreEqual(expectedLogDate, logItems[0].Time.ToString("yyyy-MM-dd HH:mm"));
            Assert.IsTrue(logItems[0].Message.StartsWith("Power was turned on."));
        }

        [TestMethod]
        public void CheckAndAct_AfterWorkingIntervalAndHome_CurrentStillTurnedOn()
        {
            // Arrange
            var config = TestFactory.NewConfig6To12();
            var powerSwitch = new TestPowerSwitch(isActive: true);
            var systemTime = new TestSystemTime(13, 0);
            var weatherForecast = new TestWeatherForecast(true, systemTime);
            var systemStartTime = systemTime.Now.AddDays(-1);
            var homeSensor = new TimeBasedHomeSensor(systemStartTime, config, powerSwitch, systemTime);
            var logger = new MowLogger();
            var rainSensor = new TestRainSensor(isWet: false);
            var mowController = new MowController(config, powerSwitch, weatherForecast, systemTime, homeSensor, logger, rainSensor);

            // Act
            mowController.CheckAndAct();

            // Assert
            Assert.AreEqual(powerSwitch.Status, PowerStatus.On);
        }

        [TestMethod]
        public void CheckAndAct_AfterWorkingIntervalAndComingHomeRightBeforeNextBadWeatherInterval_CurrentTurnedOffOnce()
        {
            // Arrange
            var systemTime = new TestSystemTime(2018, 7, 24, 12, 50);
            var config = TestFactory.NewConfig6To12And13To19();
            var powerSwitch = new TestPowerSwitch(isActive: true);
            var weatherForecast = TestFactory.NewWeatherForecastBad(systemTime);
            var systemStartTime = systemTime.Now.AddDays(-1);
            var homeSensor = new TimeBasedHomeSensor(systemStartTime, config, powerSwitch, systemTime);
            var logger = TestFactory.NewMowLogger(systemTime.Now);
            var rainSensor = new TestRainSensor(isWet: false);
            var mowController = new MowController(config, powerSwitch, weatherForecast, systemTime, homeSensor, logger, rainSensor);

            // Act
            RunOverTime(mowController, systemTime, 0, 10);

            // Assert
            Assert.AreEqual(powerSwitch.Status, PowerStatus.Off);
            Assert.IsTrue(powerSwitch.HasBeenTurnedOffOnce);

            var logItems = logger.LogItems.Where(x => x.Type == LogType.PowerOff).ToList();

            Assert.AreEqual(1, logItems.Count);
            Assert.AreEqual(LogType.PowerOff, logItems[0].Type);
            string expectedLogDate = "2018-07-24 12:55";
            Assert.AreEqual(expectedLogDate, logItems[0].Time.ToString("yyyy-MM-dd HH:mm"));
            Assert.IsTrue(logItems[0].Message.StartsWith("Power was turned off."));
        }

        [TestMethod]
        public void CheckAndAct_AfterWorkingIntervalAndComingHomeRightBeforeNextBadWeatherInterval2_CurrentTurnedOffOnce()
        {
            // Arrange
            var config = TestFactory.NewConfig6To12And13To19();
            var powerSwitch = new TestPowerSwitch(isActive: true);
            var systemTime = new TestSystemTime(2018, 7, 24, 12, 58);
            var weatherForecast = TestFactory.NewWeatherForecastBad(systemTime);
            var systemStartTime = systemTime.Now.AddDays(-1);
            var homeSensor = new TimeBasedHomeSensor(systemStartTime, config, powerSwitch, systemTime);
            var logger = TestFactory.NewMowLogger(systemTime.Now);
            var rainSensor = new TestRainSensor(isWet: false);
            var mowController = new MowController(config, powerSwitch, weatherForecast, systemTime, homeSensor, logger, rainSensor);

            // Act
            RunOverTime(mowController, systemTime, 0, 10);

            // Assert
            Assert.AreEqual(powerSwitch.Status, PowerStatus.Off);
            Assert.IsTrue(powerSwitch.HasBeenTurnedOffOnce);

            var logItems = logger.LogItems.Where(x => x.Type == LogType.PowerOff).ToList();

            Assert.AreEqual(1, logItems.Count);
            Assert.AreEqual(LogType.PowerOff, logItems[0].Type);
            string expectedLogDate = "2018-07-24 12:58";
            Assert.AreEqual(expectedLogDate, logItems[0].Time.ToString("yyyy-MM-dd HH:mm"));
            Assert.IsTrue(logItems[0].Message.StartsWith("Power was turned off."));
        }

        [TestMethod]
        public void CheckAndAct_Running24HoursInGoodWeather_CurrentNeverChanged()
        {
            // Arrange
            var config = TestFactory.NewConfig6To12();
            var powerSwitch = new TestPowerSwitch(isActive: false);
            var systemTime = new TestSystemTime(2018, 7, 24, 3, 0);
            var weatherForecast = TestFactory.NewWeatherForecastGood(systemTime);
            var systemStartTime = systemTime.Now.AddDays(-1);
            var homeSensor = new TimeBasedHomeSensor(systemStartTime, config, powerSwitch, systemTime);
            var logger = TestFactory.NewMowLogger(systemTime.Now);
            var rainSensor = new TestRainSensor(isWet: false);
            var mowController = new MowController(config, powerSwitch, weatherForecast, systemTime, homeSensor, logger, rainSensor);

            // Act
            RunOverTime(mowController, systemTime, 24, 0);

            // Assert
            Assert.AreEqual(powerSwitch.Status, PowerStatus.On);
            Assert.AreEqual(1, powerSwitch.TurnOns);
            Assert.AreEqual(0, powerSwitch.TurnOffs);
        }

        [TestMethod]
        public void CheckAndAct_Running24HoursInGoodWeatherGettingBad_CurrentTurnedOffOnce()
        {
            // Arrange
            var systemTime = new TestSystemTime(2018, 7, 24, 11, 0);
            var config = TestFactory.NewConfig0To6And12To18();
            var powerSwitch = new TestPowerSwitch(isActive: false);
            var weatherForecast = TestFactory.NewWeatherForecastGood(systemTime);
            var systemStartTime = systemTime.Now.AddDays(-1);
            var homeSensor = new TimeBasedHomeSensor(systemStartTime, config, powerSwitch, systemTime);
            var logger = TestFactory.NewMowLogger(systemTime.Now);
            var rainSensor = new TestRainSensor(isWet: false);
            var mowController = new MowController(config, powerSwitch, weatherForecast, systemTime, homeSensor, logger, rainSensor);

            // Act
            RunOverTime(mowController, systemTime, 12, 0);
            weatherForecast.SetExpectation(false);
            RunOverTime(mowController, systemTime, 12, 0);

            // Assert
            Assert.AreEqual(powerSwitch.Status, PowerStatus.On);
            Assert.AreEqual(2, powerSwitch.TurnOns);
            Assert.AreEqual(1, powerSwitch.TurnOffs);

            var logItems = logger.LogItems.Where(x => x.Type == LogType.PowerOn || x.Type == LogType.PowerOff).ToList();
            Assert.AreEqual(3, logItems.Count);

            Assert.AreEqual(LogType.PowerOff, logItems[1].Type);
            string expectedLogDate = "2018-07-24 23:55";
            Assert.AreEqual(expectedLogDate, logItems[1].Time.ToString("yyyy-MM-dd HH:mm"));
            Assert.IsTrue(logItems[1].Message.StartsWith("Power was turned off."));

            logItems = logger.LogItems.Where(x => x.Type == LogType.MowingStarted || x.Type == LogType.MowingEnded).ToList();
            Assert.AreEqual(2, logItems.Count);

            Assert.AreEqual(LogType.MowingStarted, logItems[0].Type);
            Assert.AreEqual("2018-07-24 12:00", logItems[0].Time.ToString("yyyy-MM-dd HH:mm"));

            Assert.AreEqual(LogType.MowingEnded, logItems[1].Type);
            Assert.AreEqual("2018-07-24 18:00", logItems[1].Time.ToString("yyyy-MM-dd HH:mm"));
        }

        [TestMethod]
        public void CheckAndAct_Running24HoursInGoodWeatherGettingBadStartAtDay_CurrentTurnedOffOnce()
        {
            // Arrange
            var systemTime = new TestSystemTime(2018, 7, 24, 16, 0);
            var config = TestFactory.NewConfig6To12And18To2359();
            var powerSwitch = new TestPowerSwitch(isActive: false);
            var weatherForecast = TestFactory.NewWeatherForecastGood(systemTime);
            var systemStartTime = systemTime.Now.AddDays(-1);
            var homeSensor = new TimeBasedHomeSensor(systemStartTime, config, powerSwitch, systemTime);
            var logger = TestFactory.NewMowLogger(systemTime.Now);
            var rainSensor = new TestRainSensor(isWet: false);
            var mowController = new MowController(config, powerSwitch, weatherForecast, systemTime, homeSensor, logger, rainSensor);

            // Act
            RunOverTime(mowController, systemTime, 12, 0);
            weatherForecast.SetExpectation(false);
            RunOverTime(mowController, systemTime, 12, 0);

            // Assert
            Assert.AreEqual(powerSwitch.Status, PowerStatus.On);
            Assert.AreEqual(2, powerSwitch.TurnOns);
            Assert.AreEqual(1, powerSwitch.TurnOffs);

            var logItems = logger.LogItems.Where(x => x.Type == LogType.PowerOn || x.Type == LogType.PowerOff).ToList();
            Assert.AreEqual(3, logItems.Count);

            Assert.AreEqual(LogType.PowerOff, logItems[1].Type);
            string expectedLogDate = "2018-07-25 05:55";
            Assert.AreEqual(expectedLogDate, logItems[1].Time.ToString("yyyy-MM-dd HH:mm"));
            Assert.IsTrue(logItems[1].Message.StartsWith("Power was turned off."));

            logItems = logger.LogItems.Where(x => x.Type == LogType.MowingStarted || x.Type == LogType.MowingEnded).ToList();
            Assert.AreEqual(2, logItems.Count);

            Assert.AreEqual(LogType.MowingStarted, logItems[0].Type);
            Assert.AreEqual("2018-07-24 18:00", logItems[0].Time.ToString("yyyy-MM-dd HH:mm"));

            Assert.AreEqual(LogType.MowingEnded, logItems[1].Type);
            Assert.AreEqual("2018-07-24 23:59", logItems[1].Time.ToString("yyyy-MM-dd HH:mm"));
        }

        [TestMethod]
        public void CheckAndAct_Running24HoursInBadWeatherGettingGood_CurrentTurnedOffOnce()
        {
            // Arrange
            var systemTime = new TestSystemTime(2018, 7, 24, 3, 0);
            var config = TestFactory.NewConfig6To12And18To2359();
            var powerSwitch = new TestPowerSwitch(isActive: true);
            var weatherForecast = TestFactory.NewWeatherForecastBad(systemTime);
            var systemStartTime = systemTime.Now.AddDays(-1);
            var homeSensor = new TimeBasedHomeSensor(systemStartTime, config, powerSwitch, systemTime);
            var logger = TestFactory.NewMowLogger(systemTime.Now);
            var rainSensor = new TestRainSensor(isWet: false);
            var mowController = new MowController(config, powerSwitch, weatherForecast, systemTime, homeSensor, logger, rainSensor);

            // Act
            RunOverTime(mowController, systemTime, 12, 0);
            weatherForecast.SetExpectation(true);
            RunOverTime(mowController, systemTime, 12, 0);

            // Assert
            Assert.AreEqual(powerSwitch.Status, PowerStatus.On);
            Assert.AreEqual(1, powerSwitch.TurnOns);
            Assert.AreEqual(1, powerSwitch.TurnOffs);

            var logItems = logger.LogItems.Where(x => x.Type == LogType.PowerOn || x.Type == LogType.PowerOff).ToList();

            Assert.AreEqual(2, logItems.Count);
            Assert.AreEqual(LogType.PowerOff, logItems[0].Type);
            string expectedLogDate = "2018-07-24 05:55";
            Assert.AreEqual(expectedLogDate, logItems[0].Time.ToString("yyyy-MM-dd HH:mm"));
            Assert.IsTrue(logItems[0].Message.StartsWith("Power was turned off."));

            Assert.AreEqual(LogType.PowerOn, logItems[1].Type);
            expectedLogDate = "2018-07-24 12:05";
            Assert.AreEqual(expectedLogDate, logItems[1].Time.ToString("yyyy-MM-dd HH:mm"));
            Assert.IsTrue(logItems[1].Message.StartsWith("Power was turned on."));

            logItems = logger.LogItems.Where(x => x.Type == LogType.MowingStarted || x.Type == LogType.MowingEnded).ToList();
            Assert.AreEqual(2, logItems.Count);

            Assert.AreEqual(LogType.MowingStarted, logItems[0].Type);
            Assert.AreEqual("2018-07-24 18:00", logItems[0].Time.ToString("yyyy-MM-dd HH:mm"));

            Assert.AreEqual(LogType.MowingEnded, logItems[1].Type);
            Assert.AreEqual("2018-07-24 23:59", logItems[1].Time.ToString("yyyy-MM-dd HH:mm"));
        }

        [TestMethod]
        public void CheckAndAct_Running24HoursInBadWeatherGettingGoodStartAtDay_CurrentTurnedOffOnce()
        {
            // Arrange
            var systemTime = new TestSystemTime(2018, 7, 24, 13, 0);
            var config = TestFactory.NewConfig6To12And18To2359();
            var powerSwitch = new TestPowerSwitch(isActive: true);
            var weatherForecast = TestFactory.NewWeatherForecastBad(systemTime);
            var systemStartTime = systemTime.Now.AddDays(-1);
            var homeSensor = new TimeBasedHomeSensor(systemStartTime, config, powerSwitch, systemTime);
            var logger = TestFactory.NewMowLogger(systemTime.Now);
            var rainSensor = new TestRainSensor(isWet: false);
            var mowController = new MowController(config, powerSwitch, weatherForecast, systemTime, homeSensor, logger, rainSensor);

            // Act
            RunOverTime(mowController, systemTime, 12, 0);
            weatherForecast.SetExpectation(true);
            RunOverTime(mowController, systemTime, 12, 0);

            // Assert
            Assert.AreEqual(powerSwitch.Status, PowerStatus.On);
            Assert.AreEqual(1, powerSwitch.TurnOns);
            Assert.AreEqual(1, powerSwitch.TurnOffs);

            var logItems = logger.LogItems.Where(x => x.Type == LogType.PowerOn || x.Type == LogType.PowerOff).ToList();
            Assert.AreEqual(2, logItems.Count);

            Assert.AreEqual(LogType.PowerOff, logItems[0].Type);
            string expectedLogDate = "2018-07-24 17:55";
            Assert.AreEqual(expectedLogDate, logItems[0].Time.ToString("yyyy-MM-dd HH:mm"));
            Assert.IsTrue(logItems[0].Message.StartsWith("Power was turned off."));

            Assert.AreEqual(LogType.PowerOn, logItems[1].Type);
            expectedLogDate = "2018-07-25 00:04";
            Assert.AreEqual(expectedLogDate, logItems[1].Time.ToString("yyyy-MM-dd HH:mm"));
            Assert.IsTrue(logItems[1].Message.StartsWith("Power was turned on."));

            logItems = logger.LogItems.Where(x => x.Type == LogType.MowingStarted || x.Type == LogType.MowingEnded).ToList();
            Assert.AreEqual(2, logItems.Count);

            Assert.AreEqual(LogType.MowingStarted, logItems[0].Type);
            Assert.AreEqual("2018-07-25 06:00", logItems[0].Time.ToString("yyyy-MM-dd HH:mm"));

            Assert.AreEqual(LogType.MowingEnded, logItems[1].Type);
            Assert.AreEqual("2018-07-25 12:00", logItems[1].Time.ToString("yyyy-MM-dd HH:mm"));
        }

        [TestMethod]
        public void CheckAndAct_HasWorkedEnoughGoodWeatherAhead_PowerOffNotNeeded()
        {
            // Arrange
            var systemTime = new TestSystemTime(2018, 6, 28, 17, 55);
            var config = TestFactory.NewConfig6To12And18To2359();
            var powerSwitch = new TestPowerSwitch(isActive: true);
            var weatherForecast = TestFactory.NewWeatherForecastGood(systemTime);
            var systemStartTime = systemTime.Now.AddDays(-1);
            var homeSensor = new TimeBasedHomeSensor(systemStartTime, config, powerSwitch, systemTime);
            var logger = new MowLogger();
            var rainSensor = new TestRainSensor(isWet: false);

            logger.Write(new DateTime(2018, 6, 26, 0, 0, 0), LogType.MowControllerStarted, LogLevel.InfoMoreInteresting, "Mow controller started.");
            logger.Write(new DateTime(2018, 6, 26, 6, 0, 0), LogType.PowerOn, LogLevel.Info, "Power was turned on.");

            var mowController = new MowController(config, powerSwitch, weatherForecast, systemTime, homeSensor, logger, rainSensor);

            // Act
            mowController.CheckAndAct();

            // Assert
            Assert.AreEqual(powerSwitch.Status, PowerStatus.Off);
            Assert.AreEqual(0, powerSwitch.TurnOns);
            Assert.AreEqual(1, powerSwitch.TurnOffs);

            var logItems = logger.LogItems.Where(x => x.Type == LogType.PowerOff).ToList();

            Assert.AreEqual(1, logItems.Count);
            Assert.AreEqual(LogType.PowerOff, logItems[0].Type);
            string expectedLogDate = (new DateTime(2018, 6, 28, 17, 55, 0)).ToString("yyyy-MM-dd ") + "17:55";
            Assert.AreEqual(expectedLogDate, logItems[0].Time.ToString("yyyy-MM-dd HH:mm"));
            Assert.AreEqual("Power was turned off. Mowing not necessary.", logItems[0].Message);
        }

        [TestMethod]
        public void CheckAndAct_HasWorkedEnoughBadWeatherAhead_PowerOnNeeded()
        {
            // Arrange
            var systemTime = new TestSystemTime(2018, 6, 27, 5, 55);
            var config = TestFactory.NewConfig6To12And18To2359();
            var powerSwitch = new TestPowerSwitch(isActive: true);

            var weatherForecast = TestFactory.NewWeatherForecastGood(systemTime);
            weatherForecast.AddExpectation(false, new DateTime(2018, 6, 28, 12, 0, 0));
            var systemStartTime = systemTime.Now.AddDays(-1);

            var homeSensor = new TimeBasedHomeSensor(systemStartTime, config, powerSwitch, systemTime);

            var logger = new MowLogger();
            logger.Write(new DateTime(2018, 06, 25, 0, 0, 0), LogType.MowControllerStarted, LogLevel.InfoMoreInteresting, "");
            logger.Write(new DateTime(2018, 06, 25, 0, 0, 0), LogType.PowerOn, LogLevel.Info, "");

            var rainSensor = new TestRainSensor(isWet: false);
            var mowController = new MowController(config, powerSwitch, weatherForecast, systemTime, homeSensor, logger, rainSensor);

            // Act
            mowController.CheckAndAct();

            // Assert
            Assert.AreEqual(powerSwitch.Status, PowerStatus.On);
            Assert.AreEqual(0, powerSwitch.TurnOns);
            Assert.AreEqual(0, powerSwitch.TurnOffs);
        }

        [TestMethod]
        public void CheckAndAct_HasWorkedEnoughGoodWeatherAhead2_PowerOffMowingNotNeeded()
        {
            // Arrange
            var systemTime = new TestSystemTime(2018, 06, 24, 3, 0);
            var config = TestFactory.NewConfig3To10And16To2300();
            var powerSwitch = new TestPowerSwitch(isActive: false);
            var weatherForecast = TestFactory.NewWeatherForecastGood(systemTime);
            var systemStartTime = systemTime.Now.AddDays(-1);
            var homeSensor = new TimeBasedHomeSensor(systemStartTime, config, powerSwitch, systemTime);

            var logger = TestFactory.NewMowLogger(new DateTime(2018, 6, 24, 3, 0, 0));

            var rainSensor = new TestRainSensor(isWet: false);
            var mowController = new MowController(config, powerSwitch, weatherForecast, systemTime, homeSensor, logger, rainSensor);

            // Act
            RunOverTime(mowController, systemTime, 72, 0); // 3 dagar

            // Assert
            Assert.AreEqual(powerSwitch.Status, PowerStatus.On);
            Assert.AreEqual(2, powerSwitch.TurnOns);
            Assert.AreEqual(1, powerSwitch.TurnOffs);

            var logItems = logger.LogItems.Where(x => x.Type == LogType.PowerOn || x.Type == LogType.PowerOff).ToList();

            Assert.AreEqual(3, logItems.Count);
            Assert.AreEqual(LogType.PowerOff, logItems[1].Type);
            Assert.AreEqual("2018-06-26 02:55", logItems[1].Time.ToString("yyyy-MM-dd HH:mm"));
            Assert.AreEqual("Power was turned off. Mowing not necessary.", logItems[1].Message);

            Assert.AreEqual(LogType.PowerOn, logItems[2].Type);
            Assert.AreEqual("2018-06-26 10:05", logItems[2].Time.ToString("yyyy-MM-dd HH:mm"));
            Assert.IsTrue(logItems[2].Message.StartsWith("Power was turned on."));
        }

        [TestMethod]
        public void CheckAndAct_HasWorkedEnoughGoodWeatherAheadLongTime_PowerOnMowingNeeded()
        {
            // Arrange
            var config = TestFactory.NewConfig3To10And16To2300();
            var powerSwitch = new TestPowerSwitch(isActive: false);
            var systemTime = new TestSystemTime(2018, 06, 24, 3, 0);
            var weatherForecast = TestFactory.NewWeatherForecastGood(systemTime);
            var systemStartTime = systemTime.Now.AddDays(-1);
            var homeSensor = new TimeBasedHomeSensor(systemStartTime, config, powerSwitch, systemTime);

            var logger = TestFactory.NewMowLogger(new DateTime(2018, 6, 24, 3, 0, 0));
            //logger.Write(new DateTime(2018, ), LogType.MowControllerStarted, "");

            var rainSensor = new TestRainSensor(isWet: false);
            var mowController = new MowController(config, powerSwitch, weatherForecast, systemTime, homeSensor, logger, rainSensor);

            // Act
            RunOverTime(mowController, systemTime, 240, 0); // 10 dagar

            // Assert
            var logItems = logger.LogItems.Where(x => x.Type == LogType.PowerOn || x.Type == LogType.PowerOff).ToList();

            Assert.AreEqual(powerSwitch.Status, PowerStatus.On);
            Assert.AreEqual(5, powerSwitch.TurnOns);
            Assert.AreEqual(4, powerSwitch.TurnOffs);
            Assert.AreEqual(9, logItems.Count);
        }

        [TestMethod]
        public void CheckAndAct_FailedToGetWeather_DontChangePower()
        {
            // Arrange
            var config = TestFactory.NewConfig3To10And16To2300();
            var powerSwitch = new TestPowerSwitch(isActive: false);
            var systemTime = new TestSystemTime(2018, 06, 24, 3, 0);

            var weatherForecast = TestFactory.NewWeatherForecastGood(systemTime);
            weatherForecast.SetFailureAndThrowException(true);
            var systemStartTime = systemTime.Now.AddDays(-1);

            var homeSensor = new TimeBasedHomeSensor(systemStartTime, config, powerSwitch, systemTime);

            var logger = TestFactory.NewMowLogger(new DateTime(2018, 6, 24, 0, 0, 0));

            var rainSensor = new TestRainSensor(isWet: false);
            var mowController = new MowController(config, powerSwitch, weatherForecast, systemTime, homeSensor, logger, rainSensor);

            // Act
            mowController.CheckAndAct();

            // Assert
            Assert.AreEqual(powerSwitch.Status, PowerStatus.Off);
            Assert.AreEqual(0, powerSwitch.TurnOns);
            Assert.AreEqual(0, powerSwitch.TurnOffs);

            Assert.AreEqual(2, logger.LogItems.Count);
            Assert.AreEqual(LogType.Failure, logger.LogItems[1].Type);
            Assert.AreEqual("2018-06-24 03:00", logger.LogItems[1].Time.ToString("yyyy-MM-dd HH:mm"));
            Assert.AreEqual("Failed to contact weather service.", logger.LogItems[1].Message);
        }

        [TestMethod]
        public void CheckAndAct_FailedToGetWeather_NotRepeated()
        {
            // Arrange
            var config = TestFactory.NewConfig3To10And16To2300();
            var powerSwitch = new TestPowerSwitch(isActive: false);
            var systemTime = new TestSystemTime(2018, 06, 24, 3, 0);

            var weatherForecast = TestFactory.NewWeatherForecastGood(systemTime);
            weatherForecast.SetFailureAndThrowException(true);
            var systemStartTime = systemTime.Now.AddDays(-1);

            var homeSensor = new TimeBasedHomeSensor(systemStartTime, config, powerSwitch, systemTime);

            var logger = TestFactory.NewMowLogger(new DateTime(2018, 6, 24, 0, 0, 0));

            var rainSensor = new TestRainSensor(isWet: false);
            var mowController = new MowController(config, powerSwitch, weatherForecast, systemTime, homeSensor, logger, rainSensor);

            // Act
            mowController.CheckAndAct();
            systemTime.TickMinutes(1);
            mowController.CheckAndAct();

            // Assert
            Assert.AreEqual(powerSwitch.Status, PowerStatus.Off);
            Assert.AreEqual(0, powerSwitch.TurnOns);
            Assert.AreEqual(0, powerSwitch.TurnOffs);

            Assert.AreEqual(2, logger.LogItems.Count);
            Assert.AreEqual(LogType.Failure, logger.LogItems[1].Type);
            Assert.AreEqual("2018-06-24 03:00", logger.LogItems[1].Time.ToString("yyyy-MM-dd HH:mm"));
            Assert.AreEqual("Failed to contact weather service.", logger.LogItems[1].Message);
        }

        [TestMethod]
        public void CheckAndAct_ComingHome_LogMessageSaved()
        {
            // Arrange
            var systemTime = new TestSystemTime(2018, 06, 24, 4, 30);
            var config = TestFactory.NewConfig3To10And16To2300();
            var powerSwitch = new TestPowerSwitch(isActive: true);

            var weatherForecast = TestFactory.NewWeatherForecastGood(systemTime);

            var homeSensor = new TestHomeSensor(systemTime, false);

            var logger = TestFactory.NewMowLogger(new DateTime(2018, 6, 24, 0, 0, 0));

            var rainSensor = new TestRainSensor(isWet: false);
            var mowController = new MowController(config, powerSwitch, weatherForecast, systemTime, homeSensor, logger, rainSensor);

            mowController.CheckAndAct();
            systemTime.TickMinutes(1);
            homeSensor.SetIsHome(true);

            // Act
            mowController.CheckAndAct();

            // Assert
            Assert.AreEqual(powerSwitch.Status, PowerStatus.On);
            Assert.AreEqual(0, powerSwitch.TurnOns);
            Assert.AreEqual(0, powerSwitch.TurnOffs);

            var logItems = logger.LogItems.Where(x => x.Type == LogType.MowerCame).ToList().ToList();

            Assert.AreEqual(1, logItems.Count);
            Assert.AreEqual(LogType.MowerCame, logItems[0].Type);
            Assert.AreEqual("2018-06-24 04:31", logItems[0].Time.ToString("yyyy-MM-dd HH:mm"));
        }

        [TestMethod]
        public void CheckAndAct_NotComingHome_LogMessageTellingMowerIsLost()
        {
            // Arrange
            var config = TestFactory.NewConfig3To10And16To2300(usingContactHomeSensor: true);
            var powerSwitch = new TestPowerSwitch(isActive: true);
            var systemTime = new TestSystemTime(2018, 06, 24, 3, 30);
            var weatherForecast = TestFactory.NewWeatherForecastGood(systemTime);
            var homeSensor = new TestHomeSensor(systemTime, true);
            var logger = TestFactory.NewMowLogger(new DateTime(2018, 6, 24, 0, 0, 0));

            var rainSensor = new TestRainSensor(isWet: false);
            var mowController = new MowController(config, powerSwitch, weatherForecast, systemTime, homeSensor, logger, rainSensor);

            homeSensor.SetIsHome(false); // Mower leaves
            mowController.CheckAndAct(); // Sets mower to away
            systemTime.TickMinutes(123); // Mote time 123 minutes ahead

            // Act
            mowController.CheckAndAct(); // Should see that mower has been lost

            // Assert
            var logItems = logger.LogItems.Where(x => x.Type == LogType.MowerLost).ToList();

            Assert.AreEqual(1, logItems.Count);
            Assert.AreEqual(LogType.MowerLost, logItems[0].Type);
            Assert.AreEqual("2018-06-24 05:33", logItems[0].Time.ToString("yyyy-MM-dd HH:mm"));
        }

        [TestMethod]
        public void CheckAndAct_NotLeavingHome_LogMessageTellingMowerNeverLeft()
        {
            // Arrange
            var systemTime = new TestSystemTime(2018, 06, 24, 18, 0);
            var config = TestFactory.NewConfig3To10And16To2300(
                usingContactHomeSensor: true, 
                maxChargingHours: 2);
            var powerSwitch = new TestPowerSwitch(isActive: true);
            var weatherForecast = TestFactory.NewWeatherForecastGood(systemTime);
            var homeSensor = new TestHomeSensor(systemTime, true);
            var logger = TestFactory.NewMowLogger(new DateTime(2018, 6, 24, 0, 0, 0));

            var rainSensor = new TestRainSensor(isWet: false);
            var mowController = new MowController(config, powerSwitch, weatherForecast, systemTime, homeSensor, logger, rainSensor);

            // Act
            mowController.CheckAndAct(); // Should see that mower seems to be stuck in home

            // Assert
            var logItems = logger.LogItems.Where(x => x.Type == LogType.MowerStuckInHome).ToList();

            Assert.AreEqual(1, logItems.Count);
            Assert.AreEqual(LogType.MowerLost, logItems[0].Type);
            Assert.AreEqual("2018-06-24 18:10", logItems[0].Time.ToString("yyyy-MM-dd HH:mm"));
        }

        [TestMethod]
        public void CheckAndAct_NotComingHome_LogMessageNotRepeated()
        {
            // Arrange
            var config = TestFactory.NewConfig3To10And16To2300(usingContactHomeSensor: true);
            var powerSwitch = new TestPowerSwitch(isActive: true);
            var systemTime = new TestSystemTime(2018, 06, 24, 3, 30);
            var weatherForecast = TestFactory.NewWeatherForecastGood(systemTime);
            var homeSensor = new TestHomeSensor(systemTime, true);
            var logger = TestFactory.NewMowLogger(new DateTime(2018, 6, 24, 0, 0, 0));
            var rainSensor = new TestRainSensor(isWet: false);
            var mowController = new MowController(config, powerSwitch, weatherForecast, systemTime, homeSensor, logger, rainSensor);

            homeSensor.SetIsHome(false); // Mower leaves
            mowController.CheckAndAct(); // Sets mower to away
            systemTime.TickMinutes(123); // Mote time 123 minutes ahead
            mowController.CheckAndAct();
            systemTime.TickMinutes(1);

            // Act
            mowController.CheckAndAct(); // Should see that mower has been lost, but not set the log message again.

            // Assert
            var logItems = logger.LogItems.Where(x => x.Type == LogType.MowerLost).ToList();

            Assert.AreEqual(1, logItems.Count);
        }

        [TestMethod]
        public void CheckAndAct_NotComingHomeAndNotUsingContactSensor_LostLogMessageNotWritten()
        {
            // Arrange
            var config = TestFactory.NewConfig3To10And16To2300(usingContactHomeSensor: false);
            var powerSwitch = new TestPowerSwitch(isActive: true);
            var systemTime = new TestSystemTime(2018, 06, 24, 3, 30);
            var weatherForecast = TestFactory.NewWeatherForecastGood(systemTime);
            var homeSensor = new TestHomeSensor(systemTime, true);
            var logger = TestFactory.NewMowLogger(new DateTime(2018, 6, 24, 0, 0, 0));
            var rainSensor = new TestRainSensor(isWet: false);
            var mowController = new MowController(config, powerSwitch, weatherForecast, systemTime, homeSensor, logger, rainSensor);

            homeSensor.SetIsHome(false); // Mower leaves
            mowController.CheckAndAct(); // Sets mower to away
            systemTime.TickMinutes(123); // Mote time 123 minutes ahead

            // Act
            mowController.CheckAndAct(); // Should see that mower has been lost

            // Assert
            var logItems = logger.LogItems.Where(x => x.Type == LogType.MowerLost).ToList();

            Assert.AreEqual(0, logItems.Count);
        }

        [TestMethod]
        public void CheckAndAct_BadWeatherBetweenTwoIntervals_CurrentTurnedOn()
        {
            // Arrange
            var config = TestFactory.NewConfig3To10And16To2300();
            var systemTime = new TestSystemTime(2018, 7, 24, 10, 5);
            var powerSwitch = new TestPowerSwitch(isActive: false);
            var weatherForecast = TestFactory.NewWeatherForecastBad(systemTime);
            var homeSensor = new TestHomeSensor(systemTime, true);
            var logger = TestFactory.NewMowLogger(systemTime.Now);
            var rainSensor = new TestRainSensor(isWet: false);
            var mowController = new MowController(config, powerSwitch, weatherForecast, systemTime, homeSensor, logger, rainSensor);

            // Act
            mowController.CheckAndAct();

            // Assert
            Assert.AreEqual(powerSwitch.Status, PowerStatus.On);
            Assert.IsFalse(powerSwitch.HasBeenTurnedOffOnce);
            Assert.IsTrue(powerSwitch.HasBeenTurnedOnOnce);

            var logItems = logger.LogItems.Where(x => x.Type == LogType.PowerOn).ToList();

            Assert.AreEqual(1, logItems.Count);
            string expectedLogDate = "2018-07-24 10:05";
            Assert.AreEqual(expectedLogDate, logItems[0].Time.ToString("yyyy-MM-dd HH:mm"));
        }

        [TestMethod]
        public void CheckAndAct_BadWeatherAtEndOfInterval_CurrentTurnedOffBeforeTnterval()
        {
            // Arrange
            var config = TestFactory.NewConfig3To10And16To2300(usingContactHomeSensor: false);
            var systemTime = new TestSystemTime(2018, 7, 24, 15, 0);
            var powerSwitch = new TestPowerSwitch(isActive: true);
            var weatherForecast = TestFactory.NewWeatherForecastGood(systemTime);
            var homeSensor = new TestHomeSensor(systemTime, isHome: true);
            var logger = TestFactory.NewMowLogger(systemTime.Now);
            var rainSensor = new TestRainSensor(isWet: false);
            var mowController = new MowController(config, powerSwitch, weatherForecast, systemTime, homeSensor, logger, rainSensor);

            weatherForecast.AddExpectation(false, new DateTime(2018, 7, 24, 21, 30, 0));

            // Act
            RunOverTime(mowController, systemTime, 10, 0);

            // Assert
            var logItems = logger.LogItems.Where(x => x.Type == LogType.PowerOff || x.Type == LogType.PowerOn).ToList();

            Assert.AreEqual(powerSwitch.Status, PowerStatus.On);
            Assert.AreEqual(2, logItems.Count);

            Assert.AreEqual(LogType.PowerOff, logItems[0].Type);
            Assert.AreEqual("2018-07-24 15:55", logItems[0].Time.ToString("yyyy-MM-dd HH:mm"));

            Assert.AreEqual(LogType.PowerOn, logItems[1].Type);
            Assert.AreEqual("2018-07-24 23:05", logItems[1].Time.ToString("yyyy-MM-dd HH:mm"));
        }

        [TestMethod]
        public void CheckAndAct_BadWeatherAtEndOfIntervalUsingContactSensor_CurrentTurnedOffInTnterval()
        {
            // Arrange
            var systemTime = new TestSystemTime(2018, 7, 24, 15, 0);
            var config = TestFactory.NewConfig3To10And16To2300(usingContactHomeSensor: true, maxMowingWithoutCharge: 2);
            var powerSwitch = new TestPowerSwitch(isActive: true);
            var weatherForecast = TestFactory.NewWeatherForecastGood(systemTime);
            var homeSensor = new TestHomeSensor(systemTime, isHome: true);
            var logger = TestFactory.NewMowLogger(systemTime.Now);
            var rainSensor = new TestRainSensor(isWet: false);
            var mowController = new MowController(config, powerSwitch, weatherForecast, systemTime, homeSensor, logger, rainSensor);

            weatherForecast.AddExpectation(false, new DateTime(2018, 7, 24, 21, 30, 0));

            // Act
            RunOverTime(mowController, systemTime, 1, 0);
            homeSensor.SetIsHome(false);
            RunOverTime(mowController, systemTime, 1, 30);
            homeSensor.SetIsHome(true);
            RunOverTime(mowController, systemTime, 1, 0);
            homeSensor.SetIsHome(false);
            RunOverTime(mowController, systemTime, 1, 30);
            homeSensor.SetIsHome(true);
            RunOverTime(mowController, systemTime, 5, 0);

            // Assert
            var logItems = logger.LogItems.Where(x => x.Type == LogType.PowerOff || x.Type == LogType.PowerOn).ToList();

            Assert.AreEqual(powerSwitch.Status, PowerStatus.On);
            Assert.AreEqual(2, logItems.Count);

            Assert.AreEqual(LogType.PowerOff, logItems[0].Type);
            Assert.AreEqual("2018-07-24 20:30", logItems[0].Time.ToString("yyyy-MM-dd HH:mm"));

            Assert.AreEqual(LogType.PowerOn, logItems[1].Type);
            Assert.AreEqual("2018-07-24 23:05", logItems[1].Time.ToString("yyyy-MM-dd HH:mm"));
        }

        [TestMethod]
        public void CheckAndAct_PowerStatusUnknownGoodWeatherAhead_LogMessageWritten()
        {
            // Arrange
            var config = TestFactory.NewConfig3To10And16To2300(usingContactHomeSensor: true, maxMowingWithoutCharge: 2);
            var systemTime = new TestSystemTime(2018, 7, 24, 6, 0);
            var powerSwitch = new TestPowerSwitch(PowerStatus.Unknown);
            var weatherForecast = TestFactory.NewWeatherForecastGood(systemTime);
            var homeSensor = new TestHomeSensor(systemTime, isHome: true);
            var logger = TestFactory.NewMowLogger(systemTime.Now);
            var rainSensor = new TestRainSensor(isWet: false);
            var mowController = new MowController(config, powerSwitch, weatherForecast, systemTime, homeSensor, logger, rainSensor);

            // Act
            mowController.CheckAndAct();

            // Assert
            var logItems = logger.LogItems.Where(x => x.Type == LogType.PowerOn || x.Type == LogType.PowerOff).ToList();

            Assert.AreEqual(powerSwitch.Status, PowerStatus.On);
            Assert.AreEqual(1, logItems.Count);

            Assert.AreEqual(LogType.PowerOn, logItems[0].Type);
            Assert.AreEqual("2018-07-24 06:00", logItems[0].Time.ToString("yyyy-MM-dd HH:mm"));
        }

        [TestMethod]
        public void CheckAndAct_PowerStatusUnknownBadWeatherAhead_LogMessageWritten()
        {
            // Arrange
            var config = TestFactory.NewConfig3To10And16To2300(
                usingContactHomeSensor: true, 
                maxMowingWithoutCharge: 2);
            var systemTime = new TestSystemTime(2018, 7, 24, 6, 0);
            var powerSwitch = new TestPowerSwitch(PowerStatus.Unknown);
            var weatherForecast = TestFactory.NewWeatherForecastBad(systemTime);
            var homeSensor = new TestHomeSensor(systemTime, isHome: true);
            var logger = TestFactory.NewMowLogger(systemTime.Now);
            var rainSensor = new TestRainSensor(isWet: false);
            var mowController = new MowController(config, powerSwitch, weatherForecast, systemTime, homeSensor, logger, rainSensor);

            // Act
            mowController.CheckAndAct();

            // Assert
            var logItems = logger.LogItems.Where(x => x.Type == LogType.PowerOn || x.Type == LogType.PowerOff).ToList();

            Assert.AreEqual(powerSwitch.Status, PowerStatus.Off);
            Assert.AreEqual(1, logItems.Count);

            Assert.AreEqual(LogType.PowerOff, logItems[0].Type);
            Assert.AreEqual("2018-07-24 06:00", logItems[0].Time.ToString("yyyy-MM-dd HH:mm"));
        }

        [TestMethod]
        public void CheckAndAct_PowerStatusUnknownBadWeatherAheadBetweenIntervals_LogMessageWritten()
        {
            // Arrange
            var config = TestFactory.NewConfig3To10And16To2300(
                usingContactHomeSensor: true,
                maxMowingWithoutCharge: 2);
            var systemTime = new TestSystemTime(2018, 7, 24, 2, 30);
            var powerSwitch = new TestPowerSwitch(PowerStatus.Unknown);
            var weatherForecast = TestFactory.NewWeatherForecastBad(systemTime);
            var homeSensor = new TestHomeSensor(systemTime, isHome: true);
            var logger = TestFactory.NewMowLogger(systemTime.Now);
            var rainSensor = new TestRainSensor(isWet: false);
            var mowController = new MowController(config, powerSwitch, weatherForecast, systemTime, homeSensor, logger, rainSensor);

            // Act
            mowController.CheckAndAct();

            // Assert
            var logItems = logger.LogItems.Where(x => x.Type == LogType.PowerOn || x.Type == LogType.PowerOff).ToList();

            Assert.AreEqual(powerSwitch.Status, PowerStatus.On);
            Assert.AreEqual(1, logItems.Count);

            Assert.AreEqual(LogType.PowerOn, logItems[0].Type);
            Assert.AreEqual("2018-07-24 02:30", logItems[0].Time.ToString("yyyy-MM-dd HH:mm"));
        }

        [TestMethod]
        public void CheckAndAct_PowerStatusUnknownBadWeatherAheadBetweenIntervalsAndNotHome_LogMessageWritten()
        {
            // Arrange
            var config = TestFactory.NewConfig3To10And16To2300(
                usingContactHomeSensor: true,
                maxMowingWithoutCharge: 2);
            var systemTime = new TestSystemTime(2018, 7, 24, 2, 30);
            var powerSwitch = new TestPowerSwitch(PowerStatus.Unknown);
            var weatherForecast = TestFactory.NewWeatherForecastBad(systemTime);
            var homeSensor = new TestHomeSensor(systemTime, isHome: false);
            var logger = TestFactory.NewMowLogger(systemTime.Now);
            var rainSensor = new TestRainSensor(isWet: false);
            var mowController = new MowController(config, powerSwitch, weatherForecast, systemTime, homeSensor, logger, rainSensor);

            // Act
            mowController.CheckAndAct();

            // Assert
            var logItems = logger.LogItems.Where(x => x.Type == LogType.PowerOn || x.Type == LogType.PowerOff).ToList();

            Assert.AreEqual(powerSwitch.Status, PowerStatus.On);
            Assert.AreEqual(1, logItems.Count);

            Assert.AreEqual(LogType.PowerOn, logItems[0].Type);
            Assert.AreEqual("2018-07-24 02:30", logItems[0].Time.ToString("yyyy-MM-dd HH:mm"));
        }

        [TestMethod]
        public void CheckAndAct_MowIntervalStarts_LogMessageWritten()
        {
            // Arrange
            var config = TestFactory.NewConfig3To10And16To2300();
            var systemTime = new TestSystemTime(2018, 7, 24, 3, 0);
            var powerSwitch = new TestPowerSwitch(PowerStatus.On);
            var weatherForecast = TestFactory.NewWeatherForecastGood(systemTime);
            var homeSensor = new TestHomeSensor(systemTime, isHome: true);
            var logger = TestFactory.NewMowLogger(systemTime.Now);
            var rainSensor = new TestRainSensor(isWet: false);
            var mowController = new MowController(config, powerSwitch, weatherForecast, systemTime, homeSensor, logger, rainSensor);

            // Act
            mowController.CheckAndAct();

            // Assert
            var logItems = logger.LogItems.Where(x => x.Type == LogType.MowingStarted || x.Type == LogType.MowingEnded).ToList();

            Assert.AreEqual(powerSwitch.Status, PowerStatus.On);
            Assert.AreEqual(1, logItems.Count);

            Assert.AreEqual(LogType.MowingStarted, logItems[0].Type);
            Assert.AreEqual("2018-07-24 03:00", logItems[0].Time.ToString("yyyy-MM-dd HH:mm"));
        }

        [TestMethod]
        public void CheckAndAct_MowingEndsWhenIntervalEndsAndMowerHome_LogMessageWritten()
        {
            // Arrange
            var systemTime = new TestSystemTime(2018, 7, 24, 6, 0);
            var config = TestFactory.NewConfig0To6And12To18();
            var powerSwitch = new TestPowerSwitch(PowerStatus.On);
            var weatherForecast = TestFactory.NewWeatherForecastGood(systemTime);
            var homeSensor = new TestHomeSensor(systemTime, isHome: true);
            var logger = TestFactory.NewMowLogger(systemTime.Now);
            var rainSensor = new TestRainSensor(isWet: false);
            var mowController = new MowController(config, powerSwitch, weatherForecast, systemTime, homeSensor, logger, rainSensor);

            // Act
            mowController.CheckAndAct();

            // Assert
            var logItems = logger.LogItems.Where(x => x.Type == LogType.MowingStarted || x.Type == LogType.MowingEnded).ToList();

            Assert.AreEqual(powerSwitch.Status, PowerStatus.On);
            Assert.AreEqual(1, logItems.Count);

            Assert.AreEqual(LogType.MowingEnded, logItems[0].Type);
            Assert.AreEqual("2018-07-24 06:00", logItems[0].Time.ToString("yyyy-MM-dd HH:mm"));
        }

        [TestMethod]
        public void CheckAndAct_MowingEndsWhenIntervalEndsAndMowerAway_LogMessageWritten()
        {
            // Arrange
            var systemTime = new TestSystemTime(2018, 7, 24, 6, 0);
            var config = TestFactory.NewConfig0To6And12To18();
            var powerSwitch = new TestPowerSwitch(PowerStatus.On);
            var weatherForecast = TestFactory.NewWeatherForecastGood(systemTime);
            var homeSensor = new TestHomeSensor(systemTime,
                isHome: false, 
                mowerLeftTime: new DateTime(2018, 7, 24, 4, 35, 0));
            var logger = TestFactory.NewMowLogger(systemTime.Now);
            var rainSensor = new TestRainSensor(isWet: false);
            var mowController = new MowController(config, powerSwitch, weatherForecast, systemTime, homeSensor, logger, rainSensor);

            // Act
            mowController.CheckAndAct();

            // Assert
            var logItems = logger.LogItems.Where(x => x.Type == LogType.MowingStarted || x.Type == LogType.MowingEnded).ToList();

            Assert.AreEqual(powerSwitch.Status, PowerStatus.On);
            Assert.AreEqual(1, logItems.Count);

            Assert.AreEqual(LogType.MowingEnded, logItems[0].Type);
            Assert.AreEqual("2018-07-24 06:00", logItems[0].Time.ToString("yyyy-MM-dd HH:mm"));
        }

        [TestMethod]
        public void CheckAndAct_MowingStartsInAnInterval_LogMessageWritten()
        {
            // Arrange
            var systemTime = new TestSystemTime(2018, 7, 24, 5, 0);
            var config = TestFactory.NewConfig0To6And12To18(usingContactHomeSensor: true);
            var powerSwitch = new TestPowerSwitch(PowerStatus.Off);
            var weatherForecast = TestFactory.NewWeatherForecastGood(systemTime);
            var homeSensor = new TestHomeSensor(systemTime,
                isHome: true,
                mowerLeftTime: new DateTime(2018, 7, 24, 3, 30, 0));
            var logger = TestFactory.NewMowLogger(systemTime.Now);
            var rainSensor = new TestRainSensor(isWet: false);
            var mowController = new MowController(config, powerSwitch, weatherForecast, systemTime, homeSensor, logger, rainSensor);

            // Act
            mowController.CheckAndAct();

            // Assert
            var logItems = logger.LogItems.Where(x => x.Type == LogType.MowingStarted || x.Type == LogType.MowingEnded).ToList();

            Assert.AreEqual(powerSwitch.Status, PowerStatus.On);
            Assert.AreEqual(1, logItems.Count);

            Assert.AreEqual(LogType.MowingStarted, logItems[0].Type);
            Assert.AreEqual("2018-07-24 05:00", logItems[0].Time.ToString("yyyy-MM-dd HH:mm"));
        }

        [TestMethod]
        public void CheckAndAct_MowingEndsInAnInterval_LogMessageWritten()
        {
            // Arrange
            var systemTime = new TestSystemTime(2018, 7, 24, 5, 0);
            var config = TestFactory.NewConfig0To6And12To18(usingContactHomeSensor: true);
            var powerSwitch = new TestPowerSwitch(PowerStatus.On);
            var weatherForecast = TestFactory.NewWeatherForecastBad(systemTime);
            var homeSensor = new TestHomeSensor(systemTime,
                isHome: true,
                mowerCameTime: new DateTime(2018, 7, 24, 4, 25, 0));
            var logger = TestFactory.NewMowLogger(systemTime.Now);
            var rainSensor = new TestRainSensor(isWet: false);
            var mowController = new MowController(config, powerSwitch, weatherForecast, systemTime, homeSensor, logger, rainSensor);

            // Act
            mowController.CheckAndAct();

            // Assert
            var logItems = logger.LogItems.Where(x => x.Type == LogType.MowingStarted || x.Type == LogType.MowingEnded).ToList();

            Assert.AreEqual(powerSwitch.Status, PowerStatus.Off);
            Assert.AreEqual(1, logItems.Count);

            Assert.AreEqual(LogType.MowingEnded, logItems[0].Type);
            Assert.AreEqual("2018-07-24 05:00", logItems[0].Time.ToString("yyyy-MM-dd HH:mm"));
        }

        [TestMethod]
        public void CheckAndAct_MowingEndsWhenMowerIsLost_LogMessageWritten()
        {
            // Arrange
            var systemTime = new TestSystemTime(2018, 7, 24, 5, 30);
            var config = TestFactory.NewConfig0To6And12To18(usingContactHomeSensor: true);
            var powerSwitch = new TestPowerSwitch(PowerStatus.On);
            var weatherForecast = TestFactory.NewWeatherForecastBad(systemTime);
            var homeSensor = new TestHomeSensor(systemTime,
                isHome: false,
                mowerLeftTime: new DateTime(2018, 7, 24, 3, 30, 0));
            var logger = TestFactory.NewMowLogger(new DateTime(2018, 7, 24, 0, 0, 0));
            var rainSensor = new TestRainSensor(isWet: false);
            var mowController = new MowController(config, powerSwitch, weatherForecast, systemTime, homeSensor, logger, rainSensor);

            logger.Write(new DateTime(2018, 7, 24, 3, 30, 0), LogType.MowerLeft, LogLevel.Info, "Mower left");

            // Act
            mowController.CheckAndAct();

            // Assert
            var logItems = logger.LogItems.Where(x => x.Type == LogType.MowingStarted || x.Type == LogType.MowingEnded).ToList();

            Assert.AreEqual(powerSwitch.Status, PowerStatus.On);
            Assert.AreEqual(1, logItems.Count);

            Assert.AreEqual(LogType.MowingEnded, logItems[0].Type);
            Assert.AreEqual("2018-07-24 05:30", logItems[0].Time.ToString("yyyy-MM-dd HH:mm"));
        }

        [TestMethod]
        public void CheckAndAct_MowingStartsWhenMowerIsFound_LogMessageWritten()
        {
            // Arrange
            var systemTime = new TestSystemTime(2018, 7, 24, 5, 30);
            var config = TestFactory.NewConfig0To6And12To18(usingContactHomeSensor: true);
            var powerSwitch = new TestPowerSwitch(PowerStatus.On);
            var weatherForecast = TestFactory.NewWeatherForecastGood(systemTime);
            var homeSensor = new TestHomeSensor(systemTime,
                isHome: false,
                mowerLeftTime: new DateTime(2018, 7, 24, 2, 0, 0));
            var logger = TestFactory.NewMowLogger(new DateTime(2018, 7, 24, 0, 0, 0));
            var rainSensor = new TestRainSensor(isWet: false);
            var mowController = new MowController(config, powerSwitch, weatherForecast, systemTime, homeSensor, logger, rainSensor);

            logger.Write(new DateTime(2018, 7, 24, 2, 0, 0), LogType.MowerLeft, LogLevel.Info, "Mower left");
            logger.Write(new DateTime(2018, 7, 24, 2, 0, 0), LogType.MowerLost, LogLevel.Info, "Mower lost");
            logger.Write(new DateTime(2018, 7, 24, 2, 0, 0), LogType.MowingEnded, LogLevel.Info, "Mowing ended");

            mowController.CheckAndAct();
            homeSensor.SetIsHome(true);

            // Act
            mowController.CheckAndAct();

            // Assert
            var logItems = logger.LogItems.Where(x => x.Type == LogType.MowingStarted).ToList();

            Assert.AreEqual(powerSwitch.Status, PowerStatus.On);
            Assert.AreEqual(1, logItems.Count);

            Assert.AreEqual(LogType.MowingStarted, logItems[0].Type);
            Assert.AreEqual("2018-07-24 05:30", logItems[0].Time.ToString("yyyy-MM-dd HH:mm"));
        }

        [TestMethod]
        public void CheckAndAct_LateNightBadWeatherInTheMorning_NoBothTurnOnAndOrTurnOff()
        {
            // Arrange
            var systemTime = new TestSystemTime(2018, 7, 24, 23, 25);
            var config = TestFactory.NewConfig6To12And18To2359(usingContactHomeSensor: true);
            var powerSwitch = new TestPowerSwitch(PowerStatus.Off);
            var weatherForecast = TestFactory.NewWeatherForecastGood(systemTime);
            var homeSensor = new TestHomeSensor(systemTime, 
                isHome: true,
                mowerCameTime: new DateTime(2018, 7, 24, 21, 0, 0));
            var logger = TestFactory.NewMowLogger(systemTime.Now);
            var rainSensor = new TestRainSensor(isWet: false);
            var mowController = new MowController(config, powerSwitch, weatherForecast, systemTime, homeSensor, logger, rainSensor);

            weatherForecast.AddExpectation(false, new DateTime(2018, 7, 25, 1, 0, 0));

            // Act
            mowController.CheckAndAct();

            // Assert
            Assert.AreEqual(powerSwitch.Status, PowerStatus.Off);
            Assert.AreEqual(0, powerSwitch.TurnOffs);
            Assert.AreEqual(0, powerSwitch.TurnOns);
        }

        [TestMethod]
        public void RunningOverTime_OneDayOrdinaryMowing_CorrectDailyReport()
        {
            // Arrange
            var systemTime = new TestSystemTime(2018, 7, 24, 0, 0);
            var config = TestFactory.NewConfig6To12And18To2359(usingContactHomeSensor: true);
            var powerSwitch = new TestPowerSwitch(PowerStatus.On);
            var weatherForecast = TestFactory.NewWeatherForecastGood(systemTime);
            var homeSensor = new TestHomeSensor(systemTime, isHome: true);
            var logger = TestFactory.NewMowLogger(systemTime.Now);
            var rainSensor = new TestRainSensor(isWet: false);
            var mowController = new MowController(config, powerSwitch, weatherForecast, systemTime, homeSensor, logger, rainSensor);

            // Act
            RunOverTime(mowController, systemTime, 8, 0);
            homeSensor.SetIsHome(false);
            RunOverTime(mowController, systemTime, 2, 0);
            homeSensor.SetIsHome(true);
            RunOverTime(mowController, systemTime, 14, 5);

            // Assert
            var daySummaryItems = logger.LogItems.Where(x => x.Type == LogType.DailyReport).ToList();

            Assert.AreEqual(1, daySummaryItems.Count);

            string expected = @"Mowing Summary 2018-07-24

                              06:00 Mowing started.
                              12:00 Mowing ended.
                              18:00 Mowing started.
                              23:59 Mowing ended.

                              Total mowed: 11:59 hours.
                              ".Replace(" ", "");

            string actual = daySummaryItems[0].Message.Replace(" ", "");

            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void CheckAndAct_WetAfterRain_WaitingForDryUpBeforeStart()
        {
            // Arrange
            var systemTime = new TestSystemTime(2018, 7, 24, 6, 0);
            var config = TestFactory.NewConfig6To12And18To2359(usingContactHomeSensor: true);
            var powerSwitch = new TestPowerSwitch(PowerStatus.Off);
            var weatherForecast = TestFactory.NewWeatherForecastGood(systemTime);
            var homeSensor = new TestHomeSensor(systemTime, isHome: true);
            var logger = TestFactory.NewMowLogger(systemTime.Now);
            var rainSensor = new TestRainSensor(isWet: true);
            var mowController = new MowController(config, powerSwitch, weatherForecast, systemTime, homeSensor, logger, rainSensor);

            // Act
            mowController.CheckAndAct();

            // Assert
            Assert.AreEqual(powerSwitch.Status, PowerStatus.Off);
            Assert.AreEqual(0, powerSwitch.TurnOns);
            Assert.AreEqual(0, powerSwitch.TurnOffs);
        }

        [TestMethod]
        public void CheckAndAct_WetButMowingInGoodForecast_TurnOff()
        {
            // Arrange
            var systemTime = new TestSystemTime(2018, 7, 24, 10, 0);
            var config = TestFactory.NewConfig6To12And18To2359(usingContactHomeSensor: true);
            var powerSwitch = new TestPowerSwitch(PowerStatus.On);
            var weatherForecast = TestFactory.NewWeatherForecastGood(systemTime);
            var homeSensor = new TestHomeSensor(systemTime,
                isHome: true, 
                mowerCameTime: new DateTime(2018, 7, 24, 9, 20, 0));
            var logger = TestFactory.NewMowLogger(systemTime.Now);
            var rainSensor = new TestRainSensor(isWet: true);
            var mowController = new MowController(config, powerSwitch, weatherForecast, systemTime, homeSensor, logger, rainSensor);

            // Act
            mowController.CheckAndAct();

            // Assert
            Assert.AreEqual(powerSwitch.Status, PowerStatus.Off);
            Assert.AreEqual(0, powerSwitch.TurnOns);
            Assert.AreEqual(1, powerSwitch.TurnOffs);

            var logItems = logger.LogItems.Where(x => x.Type == LogType.PowerOff || x.Type == LogType.PowerOn).ToList();

            Assert.AreEqual(1, logItems.Count);
            Assert.AreEqual(LogType.PowerOff, logItems[0].Type);
            Assert.AreEqual("2018-07-24 10:00", logItems[0].Time.ToString("yyyy-MM-dd HH:mm"));

            logItems = logger.LogItems.Where(x => x.Type == LogType.MowingStarted || x.Type == LogType.MowingEnded).ToList();

            Assert.AreEqual(1, logItems.Count);
            Assert.AreEqual(LogType.MowingEnded, logItems[0].Type);
            Assert.AreEqual("2018-07-24 10:00", logItems[0].Time.ToString("yyyy-MM-dd HH:mm"));
        }
    }
}
