using Microsoft.VisualStudio.TestTools.UnitTesting;
using MowControl;
using System;
using System.Collections.Generic;
using System.Linq;
using DanielEiserman.Power;

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
            var systemTime = new TestSystemTime(2018, 08, 23, 13, 0);
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
        public void RunningOverTime_AfterWorkingIntervalAndComingHomeRightBeforeNextBadWeatherInterval_CurrentTurnedOffOnce()
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
        public void RunningOverTime_AfterWorkingIntervalAndComingHomeRightBeforeNextBadWeatherInterval2_CurrentTurnedOffOnce()
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
        public void RunningOverTime_24HoursInGoodWeather_CurrentNeverChanged()
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
        public void RunningOverTime_24HoursInGoodWeatherGettingBad_CurrentTurnedOffOnce()
        {
            // Arrange
            var systemTime = new TestSystemTime(2018, 7, 24, 11, 0, 59);
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
        public void RunningOverTime_24HoursInGoodWeatherGettingBadStartAtDay_CurrentTurnedOffOnce()
        {
            // Arrange
            var systemTime = new TestSystemTime(2018, 7, 24, 16, 0, 12);
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
        public void RunningOverTime_24HoursInBadWeatherGettingGood_CurrentTurnedOffOnce()
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
        public void RunningOverTime_24HoursInBadWeatherGettingGoodStartAtDay_CurrentTurnedOffOnce()
        {
            // Arrange
            var systemTime = new TestSystemTime(2018, 7, 24, 13, 0, 46);
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
        public void CheckAndAct_HasWorkedEnough_PowerOffNotNeeded()
        {
            // Arrange
            var systemTime = new TestSystemTime(2018, 6, 28, 15, 30);
            var config = TestFactory.NewConfig6To12And13To19(
                usingContactHomeSensor: true,
                averageWorkPerDayHours: 8);

            var logger = TestFactory.NewMowLogger(new DateTime(2018, 6, 26, 0, 0, 0));
            logger.Write(new DateTime(2018, 6, 26, 0, 0, 0), LogType.PowerOn, LogLevel.Info, "Power was turned on.");
            logger.Write(new DateTime(2018, 6, 26, 6, 0, 0), LogType.MowingStarted, LogLevel.Info, "");
            logger.Write(new DateTime(2018, 6, 26, 12, 0, 0), LogType.MowingEnded, LogLevel.Info, "");
            logger.Write(new DateTime(2018, 6, 26, 13, 0, 0), LogType.MowingStarted, LogLevel.Info, "");
            logger.Write(new DateTime(2018, 6, 26, 19, 0, 0), LogType.MowingEnded, LogLevel.Info, "");

            logger.Write(new DateTime(2018, 6, 27, 0, 0, 0), LogType.NewDay, LogLevel.Info, "");
            logger.Write(new DateTime(2018, 6, 27, 6, 0, 0), LogType.MowingStarted, LogLevel.Info, "");
            logger.Write(new DateTime(2018, 6, 27, 12, 0, 0), LogType.MowingEnded, LogLevel.Info, "");
            logger.Write(new DateTime(2018, 6, 27, 13, 0, 0), LogType.MowingStarted, LogLevel.Info, "");
            logger.Write(new DateTime(2018, 6, 27, 19, 0, 0), LogType.MowingEnded, LogLevel.Info, "");

            logger.Write(new DateTime(2018, 6, 28, 0, 0, 0), LogType.NewDay, LogLevel.Info, "");
            logger.Write(new DateTime(2018, 6, 28, 0, 0, 0), LogType.DailyReport, LogLevel.Info, "");
            logger.Write(new DateTime(2018, 6, 28, 6, 0, 0), LogType.MowingStarted, LogLevel.Info, "");
            logger.Write(new DateTime(2018, 6, 28, 12, 0, 0), LogType.MowingEnded, LogLevel.Info, "");
            logger.Write(new DateTime(2018, 6, 28, 13, 0, 0), LogType.MowingStarted, LogLevel.Info, "");
            logger.Write(new DateTime(2018, 6, 28, 13, 0, 0), LogType.MowerLeft, LogLevel.Info, "");
            logger.Write(new DateTime(2018, 6, 28, 14, 0, 0), LogType.MowerCame, LogLevel.Info, "");

            var powerSwitch = new TestPowerSwitch(isActive: true);
            var weatherForecast = TestFactory.NewWeatherForecastGood(systemTime);
            var systemStartTime = systemTime.Now.AddDays(-2);
            var homeSensor = new TestHomeSensor(systemTime,
                isHome: true);
            var rainSensor = new TestRainSensor(isWet: false);

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
            string expectedLogDate = (new DateTime(2018, 6, 28, 15, 30, 0)).ToString("yyyy-MM-dd HH:mm");
            Assert.AreEqual(expectedLogDate, logItems[0].Time.ToString("yyyy-MM-dd HH:mm"));
            Assert.AreEqual("Power was turned off. Mowing not necessary.", logItems[0].Message);
        }

        [TestMethod]
        public void HasWorkedOverAverage_LessThanAverageCurrentDay_RunningSinceAverageIsAnEveryDayMinimum()
        {
            // Arrange
            var systemTime = new TestSystemTime(2018, 6, 27, 5, 55);
            var config = TestFactory.NewConfig6To12And13To19(
                usingContactHomeSensor: true,
                averageWorkPerDayHours: 4);

            var logger = TestFactory.NewMowLogger(new DateTime(2018, 6, 26, 0, 0, 0));
            logger.Write(new DateTime(2018, 6, 26, 0, 0, 0), LogType.PowerOn, LogLevel.Info, "Power was turned on.");
            logger.Write(new DateTime(2018, 6, 26, 6, 0, 0), LogType.MowingStarted, LogLevel.Info, "");
            logger.Write(new DateTime(2018, 6, 26, 12, 0, 0), LogType.MowingEnded, LogLevel.Info, "");
            logger.Write(new DateTime(2018, 6, 26, 13, 0, 0), LogType.MowingStarted, LogLevel.Info, "");
            logger.Write(new DateTime(2018, 6, 26, 19, 0, 0), LogType.MowingEnded, LogLevel.Info, "");
            logger.Write(new DateTime(2018, 6, 27, 0, 0, 0), LogType.NewDay, LogLevel.Info, "");

            var powerSwitch = new TestPowerSwitch(isActive: true);
            var weatherForecast = TestFactory.NewWeatherForecastGood(systemTime);
            var systemStartTime = new DateTime(2018, 6, 26, 0, 0, 0);
            var homeSensor = new TimeBasedHomeSensor(systemStartTime, config, powerSwitch, systemTime);
            var rainSensor = new TestRainSensor(isWet: false);

            var mowController = new MowController(config, powerSwitch, weatherForecast, systemTime, homeSensor, logger, rainSensor);

            // Act
            mowController.CheckAndAct();

            // Assert
            Assert.AreEqual(powerSwitch.Status, PowerStatus.On);

            var logItem = logger.LogItems.FirstOrDefault(x => x.Type == LogType.PowerOff);

            Assert.IsNull(logItem);
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
            var logger = TestFactory.NewMowLogger(new DateTime(2018, 6, 24, 0, 0, 0));
            logger.LogItems.Add(new LogItem(new DateTime(2018, 6, 24, 16, 0, 0), LogType.MowingStarted, LogLevel.Debug, ""));

            var systemTime = new TestSystemTime(2018, 06, 24, 18, 0);
            var config = TestFactory.NewConfig3To10And16To2300(
                usingContactHomeSensor: true,
                maxChargingHours: 2);
            var powerSwitch = new TestPowerSwitch(isActive: true);
            var weatherForecast = TestFactory.NewWeatherForecastGood(systemTime);
            var homeSensor = new TestHomeSensor(systemTime, true);
            var rainSensor = new TestRainSensor(isWet: false);

            var mowController = new MowController(config, powerSwitch, weatherForecast, systemTime, homeSensor, logger, rainSensor);

            // Act
            mowController.CheckAndAct(); // Should see that mower seems to be stuck in home

            // Assert
            var logItems = logger.LogItems.Where(x => x.Type == LogType.MowerStuckInHome).ToList();

            Assert.AreEqual(1, logItems.Count);
            Assert.AreEqual(LogType.MowerStuckInHome, logItems[0].Type);
            Assert.AreEqual("2018-06-24 18:00", logItems[0].Time.ToString("yyyy-MM-dd HH:mm"));
        }

        [TestMethod]
        public void MowerHomeGrassWet_CheckAndActNotLeavingHome_NoLogMessageTellingMowerNeverLeft()
        {
            // Arrange
            var config = TestFactory.NewConfig3To10And16To2300(
                usingContactHomeSensor: true,
                maxChargingHours: 2);
            var systemTime = new TestSystemTime(2018, 06, 24, 18, 0);
            var powerSwitch = new TestPowerSwitch(isActive: false);
            var weatherForecast = TestFactory.NewWeatherForecastGood(systemTime);
            var homeSensor = new TestHomeSensor(systemTime, true);
            var logger = TestFactory.NewMowLogger(new DateTime(2018, 6, 24, 0, 0, 0));

            var rainSensor = new TestRainSensor(isWet: true);
            var mowController = new MowController(config, powerSwitch, weatherForecast, systemTime, homeSensor, logger, rainSensor);

            // Act
            mowController.CheckAndAct(); // Should see that mower seems to be stuck in home

            // Assert
            var logItems = logger.LogItems.Where(x => x.Type == LogType.MowerStuckInHome).ToList();

            Assert.AreEqual(0, logItems.Count);
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
        public void CheckAndAct_BadWeatherBetweenTwoIntervalsWet_CurrentTurnedOn()
        {
            // Arrange
            var config = TestFactory.NewConfig3To10And16To2300(usingContactHomeSensor: true);
            var systemTime = new TestSystemTime(2018, 7, 24, 10, 5);
            var powerSwitch = new TestPowerSwitch(isActive: false);
            var weatherForecast = TestFactory.NewWeatherForecastBad(systemTime);
            var homeSensor = new TestHomeSensor(systemTime, true);
            var logger = TestFactory.NewMowLogger(systemTime.Now);
            var rainSensor = new TestRainSensor(isWet: true);
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
            var systemTime = new TestSystemTime(2018, 7, 24, 3, 0, 3);
            var config = TestFactory.NewConfig3To10And16To2300();
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
            var systemTime = new TestSystemTime(2018, 7, 24, 6, 0, 10);
            var config = TestFactory.NewConfig0To6And12To18();

            var logger = TestFactory.NewMowLogger(new DateTime(2018, 7, 24));
            logger.Write(new DateTime(2018, 7, 24, 0, 0, 0), LogType.PowerOn, LogLevel.Info, "Power was turned on.");
            logger.Write(new DateTime(2018, 7, 24, 0, 0, 0), LogType.MowingStarted, LogLevel.Info, "");
            logger.Write(new DateTime(2018, 7, 24, 3, 35, 0), LogType.MowerLeft, LogLevel.Info, "");
            logger.Write(new DateTime(2018, 7, 24, 5, 30, 0), LogType.MowerCame, LogLevel.Info, "");

            var powerSwitch = new TestPowerSwitch(PowerStatus.On);
            var weatherForecast = TestFactory.NewWeatherForecastGood(systemTime);
            var homeSensor = new TestHomeSensor(systemTime, isHome: true);
            var rainSensor = new TestRainSensor(isWet: false);
            var mowController = new MowController(config, powerSwitch, weatherForecast, systemTime, homeSensor, logger, rainSensor);

            // Act
            mowController.CheckAndAct();

            // Assert
            var logItem = logger.LogItems.FirstOrDefault(x => x.Type == LogType.MowingEnded);

            Assert.AreEqual(powerSwitch.Status, PowerStatus.On);
            Assert.IsNotNull(logItem);
        }

        [TestMethod]
        public void CheckAndAct_MowingEndsWhenIntervalEndsAndMowerAway_LogMessageWritten()
        {
            // Arrange
            var systemTime = new TestSystemTime(2018, 7, 24, 6, 0, 10);
            var config = TestFactory.NewConfig0To6And12To18();

            var logger = TestFactory.NewMowLogger(new DateTime(2018, 7, 24));
            logger.Write(new DateTime(2018, 7, 24, 0, 0, 0), LogType.PowerOn, LogLevel.Info, "Power was turned on.");
            logger.Write(new DateTime(2018, 7, 24, 0, 0, 0), LogType.MowingStarted, LogLevel.Info, "");
            logger.Write(new DateTime(2018, 7, 24, 4, 35, 0), LogType.MowerLeft, LogLevel.Info, "");

            var powerSwitch = new TestPowerSwitch(PowerStatus.On);
            var weatherForecast = TestFactory.NewWeatherForecastGood(systemTime);
            var homeSensor = new TestHomeSensor(systemTime,
                isHome: false, 
                mowerLeftTime: new DateTime(2018, 7, 24, 4, 35, 0));
            var rainSensor = new TestRainSensor(isWet: false);
            var mowController = new MowController(config, powerSwitch, weatherForecast, systemTime, homeSensor, logger, rainSensor);

            // Act
            mowController.CheckAndAct();

            // Assert
            var logItem = logger.LogItems.FirstOrDefault(x => x.Type == LogType.MowingEnded);

            Assert.AreEqual(powerSwitch.Status, PowerStatus.On);
            Assert.IsNotNull(logItem);
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

            logger.LogItems.Add(new LogItem(new DateTime(2018, 7, 24, 3, 30, 0), LogType.MowerLeft, LogLevel.Debug, ""));
            logger.LogItems.Add(new LogItem(new DateTime(2018, 7, 24, 5, 0, 0), LogType.MowerCame, LogLevel.Debug, ""));

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

            var logger = TestFactory.NewMowLogger(systemTime.Now);
            logger.LogItems.Add(new LogItem(new DateTime(2018, 7, 24, 0, 0, 0), LogType.MowingStarted, LogLevel.Debug, ""));

            var powerSwitch = new TestPowerSwitch(PowerStatus.On);
            var weatherForecast = TestFactory.NewWeatherForecastBad(systemTime);
            var homeSensor = new TestHomeSensor(systemTime,
                isHome: true,
                mowerCameTime: new DateTime(2018, 7, 24, 4, 25, 0));
            var rainSensor = new TestRainSensor(isWet: false);
            var mowController = new MowController(config, powerSwitch, weatherForecast, systemTime, homeSensor, logger, rainSensor);

            // Act
            mowController.CheckAndAct();

            // Assert
            Assert.AreEqual(powerSwitch.Status, PowerStatus.Off);

            var logItem = logger.LogItems.FirstOrDefault(x => x.Type == LogType.MowingEnded);
            Assert.IsNotNull(logItem);
            Assert.AreEqual("2018-07-24 05:00", logItem.Time.ToString("yyyy-MM-dd HH:mm"));
        }

        [TestMethod]
        public void CheckAndAct_MowingEndsWhenMowerIsLost_LogMessageWritten()
        {
            // Arrange
            var systemTime = new TestSystemTime(2018, 7, 24, 5, 30);
            var config = TestFactory.NewConfig0To6And12To18(usingContactHomeSensor: true);

            var logger = TestFactory.NewMowLogger(new DateTime(2018, 7, 24));
            logger.Write(new DateTime(2018, 7, 24, 0, 0, 0), LogType.PowerOn, LogLevel.Info, "Power was turned on.");
            logger.Write(new DateTime(2018, 7, 24, 0, 0, 0), LogType.MowingStarted, LogLevel.Info, "");
            logger.Write(new DateTime(2018, 7, 24, 3, 30, 0), LogType.MowerLeft, LogLevel.Info, "");

            var powerSwitch = new TestPowerSwitch(PowerStatus.On);
            var weatherForecast = TestFactory.NewWeatherForecastBad(systemTime);
            var homeSensor = new TestHomeSensor(systemTime,
                isHome: false,
                mowerLeftTime: new DateTime(2018, 7, 24, 3, 30, 0));
            var rainSensor = new TestRainSensor(isWet: false);
            var mowController = new MowController(config, powerSwitch, weatherForecast, systemTime, homeSensor, logger, rainSensor);

            // Act
            mowController.CheckAndAct();

            // Assert
            Assert.AreEqual(powerSwitch.Status, PowerStatus.On);
            var logItem = logger.LogItems.FirstOrDefault(x => x.Type == LogType.MowingEnded);
            Assert.IsNotNull(logItem);
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
            var systemTime = new TestSystemTime(2018, 7, 24, 0, 0, 45);
            var config = TestFactory.NewConfig6To12And18To2359(
                usingContactHomeSensor: true,
                averageWorkPerDayHours: 12);
            var powerSwitch = new TestPowerSwitch(PowerStatus.On);
            var weatherForecast = TestFactory.NewWeatherForecastGood(systemTime);
            var homeSensor = new TestHomeSensor(systemTime, isHome: true);
            var logger = TestFactory.NewMowLogger(systemTime.Now);
            var rainSensor = new TestRainSensor(isWet: false);
            var mowController = new MowController(config, powerSwitch, weatherForecast, systemTime, homeSensor, logger, rainSensor);

            // Act
            RunOverTime(mowController, systemTime, 6, 0);
            homeSensor.SetIsHome(false);
            RunOverTime(mowController, systemTime, 1, 30);
            homeSensor.SetIsHome(true);
            RunOverTime(mowController, systemTime, 1, 0);
            homeSensor.SetIsHome(false);
            RunOverTime(mowController, systemTime, 1, 30);
            homeSensor.SetIsHome(true);
            RunOverTime(mowController, systemTime, 8, 0);
            homeSensor.SetIsHome(false);
            RunOverTime(mowController, systemTime, 1, 30);
            homeSensor.SetIsHome(true);
            RunOverTime(mowController, systemTime, 1, 0);
            homeSensor.SetIsHome(false);
            RunOverTime(mowController, systemTime, 1, 30);
            homeSensor.SetIsHome(true);
            RunOverTime(mowController, systemTime, 1, 0);
            homeSensor.SetIsHome(false);
            RunOverTime(mowController, systemTime, 0, 59);
            homeSensor.SetIsHome(true);

            // Running into next day
            RunOverTime(mowController, systemTime, 8, 0);
            homeSensor.SetIsHome(false);
            RunOverTime(mowController, systemTime, 2, 0);
            homeSensor.SetIsHome(true);

            // Assert
            var daySummaryItems = logger.LogItems.Where(x => x.Type == LogType.DailyReport).ToList();

            Assert.AreEqual(1, daySummaryItems.Count);

            string expected = @"Mowing Summary 2018-07-24

                              06:00 Mowing started.
                              12:00 Mowing ended.
                              18:00 Mowing started.
                              23:59 Mowing ended.

                              Total mowed: 11:59 hours.
                              Actual out mowing time: 6:59 hours.
                              Exact mower away time: 6:59 hours.
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

            var logger = TestFactory.NewMowLogger(new DateTime(2018, 7, 24));
            logger.LogItems.Add(new LogItem(new DateTime(2018, 7, 24, 0, 0, 0), LogType.PowerOn, LogLevel.Debug, ""));
            logger.LogItems.Add(new LogItem(new DateTime(2018, 7, 24, 6, 0, 0), LogType.MowingStarted, LogLevel.Debug, ""));
            logger.LogItems.Add(new LogItem(new DateTime(2018, 7, 24, 8, 0, 0), LogType.MowerLeft, LogLevel.Debug, ""));
            logger.LogItems.Add(new LogItem(new DateTime(2018, 7, 24, 9, 20, 0), LogType.MowerCame, LogLevel.Debug, ""));

            var powerSwitch = new TestPowerSwitch(PowerStatus.On);
            var weatherForecast = TestFactory.NewWeatherForecastGood(systemTime);
            var homeSensor = new TestHomeSensor(systemTime,
                isHome: true, 
                mowerCameTime: new DateTime(2018, 7, 24, 9, 20, 0));
            var rainSensor = new TestRainSensor(isWet: true);
            var mowController = new MowController(config, powerSwitch, weatherForecast, systemTime, homeSensor, logger, rainSensor);

            // Act
            mowController.CheckAndAct();

            // Assert
            Assert.AreEqual(powerSwitch.Status, PowerStatus.Off);
            Assert.AreEqual(0, powerSwitch.TurnOns);
            Assert.AreEqual(1, powerSwitch.TurnOffs);

            var logItem = logger.LogItems.FirstOrDefault(x => x.Type == LogType.PowerOff);
            Assert.IsNotNull(logItem);

            logItem = logger.LogItems.FirstOrDefault(x => x.Type == LogType.MowingEnded);
            Assert.IsNotNull(logItem);
        }

        [TestMethod]
        public void CheckAndAct_PowerTurnedOnAfterBeenOffAWhile_NotReportingMowerStuck()
        {
            // Arrange
            var logger = TestFactory.NewMowLogger(new DateTime(2019, 06, 18, 0, 0, 0));
            logger.LogItems.Add(new LogItem(new DateTime(2019, 6, 18, 7, 31, 0), LogType.MowerCame, LogLevel.Debug, ""));
            logger.LogItems.Add(new LogItem(new DateTime(2019, 6, 18, 8, 22, 0), LogType.PowerOff, LogLevel.Debug, ""));
            logger.LogItems.Add(new LogItem(new DateTime(2019, 6, 18, 8, 22, 0), LogType.MowingEnded, LogLevel.Debug, ""));
            logger.LogItems.Add(new LogItem(new DateTime(2019, 6, 18, 16, 5, 0), LogType.PowerOn, LogLevel.Debug, ""));

            var systemTime = new TestSystemTime(2019, 6, 18, 16, 6);
            var config = TestFactory.NewConfig1To16And17To2359(usingContactHomeSensor: true);
            var powerSwitch = new TestPowerSwitch(PowerStatus.On);
            var weatherForecast = TestFactory.NewWeatherForecastBad(systemTime);
            var homeSensor = new TestHomeSensor(systemTime,
                isHome: true,
                mowerCameTime: new DateTime(2019, 6, 18, 7, 31, 0));
            var rainSensor = new TestRainSensor(isWet: true);
            var mowController = new MowController(config, powerSwitch, weatherForecast, systemTime, homeSensor, logger, rainSensor);

            // Act
            mowController.CheckAndAct();

            // Assert
            var logItems = logger.LogItems.Where(x => x.Type == LogType.MowerStuckInHome).ToList();

            Assert.AreEqual(powerSwitch.Status, PowerStatus.On);
            Assert.AreEqual(0, logItems.Count);
        }

        [TestMethod]
        public void CheckAndAct_PowerTurnedOnAfterBeenOffAWhileAlmostTwoHoursLaterGoodWeather_NotReportingMowerStuck()
        {
            // Arrange
            var logger = TestFactory.NewMowLogger(new DateTime(2019, 06, 18, 0, 0, 0));
            logger.LogItems.Add(new LogItem(new DateTime(2019, 6, 18, 7, 31, 0), LogType.MowerCame, LogLevel.Debug, ""));
            logger.LogItems.Add(new LogItem(new DateTime(2019, 6, 18, 8, 22, 0), LogType.PowerOff, LogLevel.Debug, ""));
            logger.LogItems.Add(new LogItem(new DateTime(2019, 6, 18, 8, 22, 0), LogType.MowingEnded, LogLevel.Debug, ""));
            logger.LogItems.Add(new LogItem(new DateTime(2019, 6, 18, 16, 5, 0), LogType.PowerOn, LogLevel.Debug, ""));

            var systemTime = new TestSystemTime(2019, 6, 18, 18, 4);
            var config = TestFactory.NewConfig1To16And17To2359(usingContactHomeSensor: true);
            var powerSwitch = new TestPowerSwitch(PowerStatus.On);
            var weatherForecast = TestFactory.NewWeatherForecastGood(systemTime);
            var homeSensor = new TestHomeSensor(systemTime,
                isHome: true,
                mowerCameTime: new DateTime(2019, 6, 18, 7, 31, 0));
            var rainSensor = new TestRainSensor(isWet: false);
            var mowController = new MowController(config, powerSwitch, weatherForecast, systemTime, homeSensor, logger, rainSensor);

            // Act
            mowController.CheckAndAct();

            // Assert
            var logItems = logger.LogItems.Where(x => x.Type == LogType.MowerStuckInHome).ToList();

            Assert.AreEqual(powerSwitch.Status, PowerStatus.On);
            Assert.AreEqual(0, logItems.Count);
        }

        [TestMethod]
        public void CheckAndAct_PowerTurnedOnAfterBeenOffAWhileTwoHoursLaterIsWet_NotReportingMowerStuck()
        {
            // Arrange
            var logger = TestFactory.NewMowLogger(new DateTime(2019, 06, 18, 0, 0, 0));
            logger.LogItems.Add(new LogItem(new DateTime(2019, 6, 18, 7, 31, 0), LogType.MowerCame, LogLevel.Debug, ""));
            logger.LogItems.Add(new LogItem(new DateTime(2019, 6, 18, 8, 22, 0), LogType.PowerOff, LogLevel.Debug, ""));
            logger.LogItems.Add(new LogItem(new DateTime(2019, 6, 18, 8, 22, 0), LogType.MowingEnded, LogLevel.Debug, ""));
            logger.LogItems.Add(new LogItem(new DateTime(2019, 6, 18, 16, 5, 0), LogType.PowerOn, LogLevel.Debug, ""));

            var systemTime = new TestSystemTime(2019, 6, 18, 18, 6);
            var config = TestFactory.NewConfig1To16And17To2359(usingContactHomeSensor: true);
            var powerSwitch = new TestPowerSwitch(PowerStatus.On);
            var weatherForecast = TestFactory.NewWeatherForecastBad(systemTime);
            var homeSensor = new TestHomeSensor(systemTime,
                isHome: true,
                mowerCameTime: new DateTime(2019, 6, 18, 7, 31, 0));
            var rainSensor = new TestRainSensor(isWet: true);
            var mowController = new MowController(config, powerSwitch, weatherForecast, systemTime, homeSensor, logger, rainSensor);

            // Act
            mowController.CheckAndAct();

            // Assert
            var logItems = logger.LogItems.Where(x => x.Type == LogType.MowerStuckInHome).ToList();

            Assert.AreEqual(powerSwitch.Status, PowerStatus.Off);
            Assert.AreEqual(0, logItems.Count);
        }

        [TestMethod]
        public void CheckAndAct_PowerTurnedOnAfterBeenOffAWhileTwoHoursLaterBadWeather_NotReportingMowerStuck()
        {
            // Arrange
            var logger = TestFactory.NewMowLogger(new DateTime(2019, 06, 18, 0, 0, 0));
            logger.LogItems.Add(new LogItem(new DateTime(2019, 6, 18, 7, 31, 0), LogType.MowerCame, LogLevel.Debug, ""));
            logger.LogItems.Add(new LogItem(new DateTime(2019, 6, 18, 8, 22, 0), LogType.PowerOff, LogLevel.Debug, ""));
            logger.LogItems.Add(new LogItem(new DateTime(2019, 6, 18, 8, 22, 0), LogType.MowingEnded, LogLevel.Debug, ""));
            logger.LogItems.Add(new LogItem(new DateTime(2019, 6, 18, 16, 5, 0), LogType.PowerOn, LogLevel.Debug, ""));

            var systemTime = new TestSystemTime(2019, 6, 18, 18, 6);
            var config = TestFactory.NewConfig1To16And17To2359(usingContactHomeSensor: true);
            var powerSwitch = new TestPowerSwitch(PowerStatus.On);
            var weatherForecast = TestFactory.NewWeatherForecastBad(systemTime);
            var homeSensor = new TestHomeSensor(systemTime,
                isHome: true,
                mowerCameTime: new DateTime(2019, 6, 18, 7, 31, 0));
            var rainSensor = new TestRainSensor(isWet: false);
            var mowController = new MowController(config, powerSwitch, weatherForecast, systemTime, homeSensor, logger, rainSensor);

            // Act
            mowController.CheckAndAct();

            // Assert
            var logItems = logger.LogItems.Where(x => x.Type == LogType.MowerStuckInHome).ToList();

            Assert.AreEqual(powerSwitch.Status, PowerStatus.Off);
            Assert.AreEqual(0, logItems.Count);
        }

        [TestMethod]
        public void CheckAndAct_PowerTurnedOnAfterBeenOffAWhileTwoHoursLaterGoodWeather_ReportingMowerStuck()
        {
            // Arrange
            var logger = TestFactory.NewMowLogger(new DateTime(2019, 06, 18, 0, 0, 0));
            logger.LogItems.Add(new LogItem(new DateTime(2019, 6, 18, 7, 31, 0), LogType.MowerCame, LogLevel.Debug, ""));
            logger.LogItems.Add(new LogItem(new DateTime(2019, 6, 18, 8, 22, 0), LogType.PowerOff, LogLevel.Debug, ""));
            logger.LogItems.Add(new LogItem(new DateTime(2019, 6, 18, 8, 22, 0), LogType.MowingEnded, LogLevel.Debug, ""));
            logger.LogItems.Add(new LogItem(new DateTime(2019, 6, 18, 16, 5, 0), LogType.PowerOn, LogLevel.Debug, ""));
            logger.LogItems.Add(new LogItem(new DateTime(2019, 6, 18, 16, 5, 0), LogType.MowingStarted, LogLevel.Debug, ""));

            var systemTime = new TestSystemTime(2019, 6, 18, 18, 5);
            var config = TestFactory.NewConfig1To16And17To2359(usingContactHomeSensor: true);
            var powerSwitch = new TestPowerSwitch(PowerStatus.On);
            var weatherForecast = TestFactory.NewWeatherForecastGood(systemTime);
            var homeSensor = new TestHomeSensor(systemTime,
                isHome: true,
                mowerCameTime: new DateTime(2019, 6, 18, 7, 31, 0));
            var rainSensor = new TestRainSensor(isWet: false);
            var mowController = new MowController(config, powerSwitch, weatherForecast, systemTime, homeSensor, logger, rainSensor);

            // Act
            mowController.CheckAndAct();

            // Assert
            var logItems = logger.LogItems.Where(x => x.Type == LogType.MowerStuckInHome && x.Time.Hour == 18).ToList();

            Assert.AreEqual(powerSwitch.Status, PowerStatus.On);
            Assert.AreEqual(1, logItems.Count);
        }

        [TestMethod]
        public void CheckAndAct_MowerLeftALittleBitBeforeIntervalStart_MowingStarted()
        {
            // Arrange
            var logger = TestFactory.NewMowLogger(new DateTime(2019, 06, 23, 0, 0, 0));
            logger.LogItems.Add(new LogItem(new DateTime(2019, 6, 23, 16, 0, 0), LogType.MowingEnded, LogLevel.Debug, ""));
            logger.LogItems.Add(new LogItem(new DateTime(2019, 6, 23, 16, 06, 0), LogType.MowerCame, LogLevel.Debug, ""));
            logger.LogItems.Add(new LogItem(new DateTime(2019, 6, 23, 16, 59, 0), LogType.MowerLeft, LogLevel.Debug, ""));

            var systemTime = new TestSystemTime(2019, 6, 23, 17, 0);
            var config = TestFactory.NewConfig1To16And17To2359(usingContactHomeSensor: true);
            var powerSwitch = new TestPowerSwitch(PowerStatus.On);
            var weatherForecast = TestFactory.NewWeatherForecastGood(systemTime);
            var homeSensor = new TestHomeSensor(systemTime,
                isHome: false,
                mowerCameTime: new DateTime(2019, 6, 23, 16, 06, 0),
                mowerLeftTime: new DateTime(2019, 6, 23, 16, 59, 0));
            var rainSensor = new TestRainSensor(isWet: false);
            var mowController = new MowController(config, powerSwitch, weatherForecast, systemTime, homeSensor, logger, rainSensor);

            // Act
            mowController.CheckAndAct();

            // Assert
            var logItems = logger.LogItems.Where(x => x.Type == LogType.MowingStarted).ToList();

            Assert.AreEqual(powerSwitch.Status, PowerStatus.On);
            Assert.AreEqual(1, logItems.Count);
        }

        [TestMethod]
        public void CheckAndAct_MowerHasBeenLostALongWhile_MowingNotStartedOnIntervalStart()
        {
            // Arrange
            var logger = TestFactory.NewMowLogger(new DateTime(2019, 06, 23, 0, 0, 0));
            logger.LogItems.Add(new LogItem(new DateTime(2019, 6, 23, 1, 0, 0), LogType.MowingStarted, LogLevel.Debug, ""));
            logger.LogItems.Add(new LogItem(new DateTime(2019, 6, 23, 1, 5, 0), LogType.MowerLeft, LogLevel.Debug, ""));
            logger.LogItems.Add(new LogItem(new DateTime(2019, 6, 23, 3, 5, 0), LogType.MowerLost, LogLevel.Debug, ""));
            logger.LogItems.Add(new LogItem(new DateTime(2019, 6, 23, 3, 5, 0), LogType.MowingEnded, LogLevel.Debug, ""));

            var systemTime = new TestSystemTime(2019, 6, 23, 17, 0);
            var config = TestFactory.NewConfig1To16And17To2359(usingContactHomeSensor: true);
            var powerSwitch = new TestPowerSwitch(PowerStatus.On);
            var weatherForecast = TestFactory.NewWeatherForecastGood(systemTime);
            var homeSensor = new TestHomeSensor(systemTime,
                isHome: false,
                mowerLeftTime: new DateTime(2019, 6, 23, 1, 5, 0));
            var rainSensor = new TestRainSensor(isWet: false);
            var mowController = new MowController(config, powerSwitch, weatherForecast, systemTime, homeSensor, logger, rainSensor);

            // Act
            mowController.CheckAndAct();

            // Assert
            var logItems = logger.LogItems.Where(x => x.Type == LogType.MowingStarted && x.Time.Hour == 17).ToList();

            Assert.AreEqual(powerSwitch.Status, PowerStatus.On);
            Assert.AreEqual(0, logItems.Count);
        }

        [TestMethod]
        public void StuckAtHome_Leaves_MowingStarted()
        {
            // Arrange
            var logger = TestFactory.NewMowLogger(new DateTime(2019, 6, 12, 0, 0, 0));
            logger.LogItems.Add(new LogItem(new DateTime(2019, 6, 12, 1, 0, 0), LogType.MowingStarted, LogLevel.Debug, ""));
            logger.LogItems.Add(new LogItem(new DateTime(2019, 6, 12, 3, 0, 0), LogType.MowerStuckInHome, LogLevel.Debug, ""));
            logger.LogItems.Add(new LogItem(new DateTime(2019, 6, 12, 3, 0, 0), LogType.MowingEnded, LogLevel.Debug, ""));

            var systemTime = new TestSystemTime(2019, 6, 12, 5, 30);
            var config = TestFactory.NewConfig1To16And17To2359(usingContactHomeSensor: true);
            var powerSwitch = new TestPowerSwitch(PowerStatus.On);
            var weatherForecast = TestFactory.NewWeatherForecastGood(systemTime);
            var homeSensor = new TestHomeSensor(systemTime,
                isHome: false,
                mowerLeftTime: new DateTime(2019, 6, 12, 5, 30, 0));
            var rainSensor = new TestRainSensor(isWet: false);
            var mowController = new MowController(config, powerSwitch, weatherForecast, systemTime, homeSensor, logger, rainSensor, mowerIsHome: true);

            // Act
            mowController.CheckAndAct();

            // Assert
            Assert.AreEqual(powerSwitch.Status, PowerStatus.On);

            var logItems = logger.LogItems.Where(x => x.Type == LogType.MowerLeft && x.Time.Hour == 5).ToList();
            Assert.AreEqual(1, logItems.Count);

            logItems = logger.LogItems.Where(x => x.Type == LogType.MowingStarted && x.Time.Hour == 5).ToList();
            Assert.AreEqual(1, logItems.Count);
        }

        [TestMethod]
        public void CheckAndAct_ContactSensorWorkedADay_DetailedDailyReport()
        {
            // Arrange
            var logger = TestFactory.NewMowLogger(new DateTime(2019, 6, 12, 0, 0, 0));
            logger.LogItems.Add(new LogItem(new DateTime(2019, 6, 12, 1, 0, 0), LogType.MowingStarted, LogLevel.Debug, ""));
            logger.LogItems.Add(new LogItem(new DateTime(2019, 6, 12, 1, 0, 0), LogType.MowerLeft, LogLevel.Debug, ""));
            logger.LogItems.Add(new LogItem(new DateTime(2019, 6, 12, 2, 30, 0), LogType.MowerCame, LogLevel.Debug, ""));
            logger.LogItems.Add(new LogItem(new DateTime(2019, 6, 12, 3, 30, 0), LogType.MowerLeft, LogLevel.Debug, ""));
            logger.LogItems.Add(new LogItem(new DateTime(2019, 6, 12, 5, 0, 0), LogType.MowerCame, LogLevel.Debug, ""));
            logger.LogItems.Add(new LogItem(new DateTime(2019, 6, 12, 5, 30, 0), LogType.PowerOff, LogLevel.Debug, ""));
            logger.LogItems.Add(new LogItem(new DateTime(2019, 6, 12, 5, 30, 0), LogType.MowingEnded, LogLevel.Debug, ""));

            var systemTime = new TestSystemTime(2019, 6, 13, 0, 0, 0);
            var config = TestFactory.NewConfig6To12And18To2359(usingContactHomeSensor: true);
            var powerSwitch = new TestPowerSwitch(PowerStatus.On);
            var weatherForecast = TestFactory.NewWeatherForecastGood(systemTime);
            var homeSensor = new TestHomeSensor(systemTime, isHome: true);
            var rainSensor = new TestRainSensor(isWet: false);
            var mowController = new MowController(config, powerSwitch, weatherForecast, systemTime, homeSensor, logger, rainSensor);

            // Act
            mowController.CheckAndAct();

            // Assert
            var daySummaryItems = logger.LogItems.Where(x => x.Type == LogType.DailyReport).ToList();

            Assert.AreEqual(1, daySummaryItems.Count);

            string expected = @"Actual out mowing time: 3:00 hours.
                              ".Replace(" ", "").Replace("\r\n", " ");

            string actual = daySummaryItems[0].Message.Replace(" ", "").Replace("\r\n", " ");

            Assert.IsTrue(actual.Contains(expected));
        }

        [TestMethod]
        public void StoodStillForHalfADayInBadWeather_LeavingChargingInterval_PowerTurnedOff()
        {
            // Arrange
            var logger = TestFactory.NewMowLogger(new DateTime(2019, 7, 16, 9, 0, 0));
            logger.LogItems.Add(new LogItem(new DateTime(2019, 7, 16, 9, 0, 0), LogType.MowingStarted, LogLevel.Debug, ""));
            logger.LogItems.Add(new LogItem(new DateTime(2019, 7, 16, 9, 16, 0), LogType.MowerCame, LogLevel.Debug, ""));
            logger.LogItems.Add(new LogItem(new DateTime(2019, 7, 16, 9, 46, 0), LogType.PowerOff, LogLevel.Debug, ""));
            logger.LogItems.Add(new LogItem(new DateTime(2019, 7, 16, 9, 46, 0), LogType.MowingEnded, LogLevel.Debug, ""));
            logger.LogItems.Add(new LogItem(new DateTime(2019, 7, 16, 16, 5, 0), LogType.PowerOn, LogLevel.Debug, ""));

            var systemTime = new TestSystemTime(2019, 7, 16, 16, 55, 0);
            var config = TestFactory.NewConfig9To16And17To2359(usingContactHomeSensor: true);
            var powerSwitch = new TestPowerSwitch(PowerStatus.On);
            var weatherForecast = TestFactory.NewWeatherForecastBad(systemTime);
            var homeSensor = new TestHomeSensor(systemTime, isHome: true);
            var rainSensor = new TestRainSensor(isWet: true);
            var mowController = new MowController(config, powerSwitch, weatherForecast, systemTime, homeSensor, logger, rainSensor);

            // Act
            mowController.CheckAndAct();

            // Assert
            Assert.AreEqual(powerSwitch.Status, PowerStatus.Off);
            var logItems = logger.LogItems.Where(x => x.Type == LogType.PowerOff && x.Time.Hour == 16).ToList();
            Assert.AreEqual(1, logItems.Count);
        }

        [TestMethod]
        public void MowerLost_NextIntervalStarting_MowingStartedShouldNotOccurr()
        {
            // Arrange
            var logger = TestFactory.NewMowLogger(new DateTime(2019, 7, 26, 0, 0, 0));
            logger.LogItems.Add(new LogItem(new DateTime(2019, 7, 26, 20, 0, 0), LogType.MowingStarted, LogLevel.Debug, ""));
            logger.LogItems.Add(new LogItem(new DateTime(2019, 7, 26, 21, 31, 0), LogType.MowerCame, LogLevel.Debug, ""));
            logger.LogItems.Add(new LogItem(new DateTime(2019, 7, 26, 22, 25, 0), LogType.MowerLeft, LogLevel.Debug, ""));
            logger.LogItems.Add(new LogItem(new DateTime(2019, 7, 26, 23, 59, 0), LogType.MowingEnded, LogLevel.Debug, ""));
            logger.LogItems.Add(new LogItem(new DateTime(2019, 7, 27, 0, 26, 0), LogType.MowerLost, LogLevel.Debug, ""));

            var systemTime = new TestSystemTime(2019, 7, 27, 10, 0);
            var config = TestFactory.NewConfig10To12And20To2359(usingContactHomeSensor: true);
            var powerSwitch = new TestPowerSwitch(PowerStatus.On);
            var weatherForecast = TestFactory.NewWeatherForecastGood(systemTime);
            var homeSensor = new TestHomeSensor(systemTime,
                isHome: true,
                mowerCameTime: new DateTime(2019, 7, 26, 21, 31, 0),
                mowerLeftTime: new DateTime(2019, 7, 26, 22, 25, 0));
            var rainSensor = new TestRainSensor(isWet: false);
            var mowController = new MowController(config, powerSwitch, weatherForecast, systemTime, homeSensor, logger, rainSensor, mowerIsHome: true);

            // Act
            mowController.CheckAndAct();

            // Assert
            Assert.AreEqual(powerSwitch.Status, PowerStatus.On);

            var logItems = logger.LogItems.Where(x => x.Type == LogType.MowingStarted && x.Time.Hour == 10).ToList();
            Assert.AreEqual(0, logItems.Count);
        }

        [TestMethod]
        public void MowerAtHomeIntervalEnds_NextIntervalStarting_MowingStartedShouldNotOccurr()
        {
            // Arrange
            var logger = TestFactory.NewMowLogger(new DateTime(2019, 7, 26, 0, 0, 0));
            logger.LogItems.Add(new LogItem(new DateTime(2019, 7, 26, 10, 0, 0), LogType.MowingStarted, LogLevel.Debug, ""));
            logger.LogItems.Add(new LogItem(new DateTime(2019, 7, 26, 11, 36, 0), LogType.MowerCame, LogLevel.Debug, ""));
            logger.LogItems.Add(new LogItem(new DateTime(2019, 7, 26, 12, 0, 0), LogType.MowingEnded, LogLevel.Debug, ""));

            var systemTime = new TestSystemTime(2019, 7, 26, 13, 36);
            var config = TestFactory.NewConfig10To12And20To2359(usingContactHomeSensor: true);
            var powerSwitch = new TestPowerSwitch(PowerStatus.On);
            var weatherForecast = TestFactory.NewWeatherForecastGood(systemTime);
            var homeSensor = new TestHomeSensor(systemTime,
                isHome: true,
                mowerCameTime: new DateTime(2019, 7, 26, 11, 36, 0));
            var rainSensor = new TestRainSensor(isWet: false);
            var mowController = new MowController(config, powerSwitch, weatherForecast, systemTime, homeSensor, logger, rainSensor, mowerIsHome: true);

            // Act
            mowController.CheckAndAct();

            // Assert
            Assert.AreEqual(powerSwitch.Status, PowerStatus.On);

            var logItems = logger.LogItems.Where(x => x.Time.Hour == 13 && x.Time.Minute == 36).ToList();
            Assert.AreEqual(0, logItems.Count);
        }

        [TestMethod]
        public void MowingStartedYesterday_CreatingDailyReport_CorrectDailyReport()
        {
            // Arrange
            var logger = TestFactory.NewMowLogger(new DateTime(2019, 8, 7, 0, 0, 0));
            logger.LogItems.Add(new LogItem(new DateTime(2019, 8, 9, 0, 0, 0), LogType.NewDay, LogLevel.Debug, ""));
            logger.LogItems.Add(new LogItem(new DateTime(2019, 8, 9, 20, 0, 0), LogType.MowingStarted, LogLevel.Debug, ""));
            logger.LogItems.Add(new LogItem(new DateTime(2019, 8, 9, 20, 30, 0), LogType.MowerLeft, LogLevel.Debug, ""));
            logger.LogItems.Add(new LogItem(new DateTime(2019, 8, 9, 21, 30, 0), LogType.MowerCame, LogLevel.Debug, ""));
            logger.LogItems.Add(new LogItem(new DateTime(2019, 8, 9, 22, 30, 0), LogType.MowerLeft, LogLevel.Debug, ""));
            logger.LogItems.Add(new LogItem(new DateTime(2019, 8, 9, 23, 59, 0), LogType.MowingEnded, LogLevel.Debug, ""));

            var systemTime = new TestSystemTime(2019, 8, 10, 0, 0, 0);
            var config = TestFactory.NewConfig10To12And20To2359(usingContactHomeSensor: true);
            var powerSwitch = new TestPowerSwitch(PowerStatus.On);
            var weatherForecast = TestFactory.NewWeatherForecastGood(systemTime);
            var homeSensor = new TestHomeSensor(systemTime, isHome: true);
            var rainSensor = new TestRainSensor(isWet: false);
            var mowController = new MowController(config, powerSwitch, weatherForecast, systemTime, homeSensor, logger, rainSensor);

            // Act
            mowController.CheckAndAct();

            // Assert
            var daySummaryItems = logger.LogItems.Where(x => x.Type == LogType.DailyReport).ToList();

            Assert.AreEqual(1, daySummaryItems.Count);

            string expectedIntervalMowingTime = @"Total mowed: 3:59 hours.
                              ".Replace(" ", "").Replace("\r\n", " ");
            string expectedActualMowingTime = @"Actual out mowing time: 2:29 hours.
                              ".Replace(" ", "").Replace("\r\n", " ");
            string expectedExactAwayTime = @"Exact mower away time: 2:29 hours.
                              ".Replace(" ", "").Replace("\r\n", " ");

            string actualReport = daySummaryItems[0].Message.Replace(" ", "").Replace("\r\n", " ");

            Assert.IsTrue(actualReport.Contains(expectedIntervalMowingTime));
            Assert.IsTrue(actualReport.Contains(expectedActualMowingTime));
            Assert.IsTrue(actualReport.Contains(expectedExactAwayTime));
        }

        [TestMethod]
        public void MowingAFewDaysSimplified_CreatingDailyReport_CorrectDailyReport()
        {
            // Arrange
            var logger = TestFactory.NewMowLogger(new DateTime(2019, 8, 8, 0, 0, 0));
            logger.LogItems.Add(new LogItem(new DateTime(2019, 8, 8, 0, 04, 0), LogType.PowerOn, LogLevel.Debug, ""));
            logger.LogItems.Add(new LogItem(new DateTime(2019, 8, 8, 11, 0, 0), LogType.MowingStarted, LogLevel.Debug, ""));
            logger.LogItems.Add(new LogItem(new DateTime(2019, 8, 8, 12, 0, 0), LogType.MowingEnded, LogLevel.Debug, ""));
            logger.LogItems.Add(new LogItem(new DateTime(2019, 8, 8, 14, 0, 0), LogType.MowerLeft, LogLevel.Debug, ""));
            logger.LogItems.Add(new LogItem(new DateTime(2019, 8, 8, 15, 0, 0), LogType.MowerCame, LogLevel.Debug, ""));
            logger.LogItems.Add(new LogItem(new DateTime(2019, 8, 8, 19, 55, 0), LogType.PowerOff, LogLevel.Debug, ""));

            logger.LogItems.Add(new LogItem(new DateTime(2019, 8, 9, 0, 04, 0), LogType.PowerOn, LogLevel.Debug, ""));
            logger.LogItems.Add(new LogItem(new DateTime(2019, 8, 9, 10, 0, 0), LogType.MowingStarted, LogLevel.Debug, ""));
            logger.LogItems.Add(new LogItem(new DateTime(2019, 8, 9, 10, 30, 0), LogType.MowerLeft, LogLevel.Debug, ""));
            logger.LogItems.Add(new LogItem(new DateTime(2019, 8, 9, 11, 30, 0), LogType.MowerCame, LogLevel.Debug, ""));
            logger.LogItems.Add(new LogItem(new DateTime(2019, 8, 9, 12, 0, 0), LogType.MowingEnded, LogLevel.Debug, ""));
            logger.LogItems.Add(new LogItem(new DateTime(2019, 8, 9, 20, 0, 0), LogType.MowingStarted, LogLevel.Debug, ""));
            logger.LogItems.Add(new LogItem(new DateTime(2019, 8, 9, 20, 30, 0), LogType.MowerLeft, LogLevel.Debug, ""));
            logger.LogItems.Add(new LogItem(new DateTime(2019, 8, 9, 21, 30, 0), LogType.MowerCame, LogLevel.Debug, ""));
            logger.LogItems.Add(new LogItem(new DateTime(2019, 8, 9, 23, 59, 0), LogType.MowingEnded, LogLevel.Debug, ""));

            var systemTime = new TestSystemTime(2019, 8, 10, 0, 0, 0);
            var config = TestFactory.NewConfig10To12And20To2359(usingContactHomeSensor: true);
            var powerSwitch = new TestPowerSwitch(PowerStatus.On);
            var weatherForecast = TestFactory.NewWeatherForecastGood(systemTime);
            var homeSensor = new TestHomeSensor(systemTime, isHome: true);
            var rainSensor = new TestRainSensor(isWet: false);
            var mowController = new MowController(config, powerSwitch, weatherForecast, systemTime, homeSensor, logger, rainSensor);

            // Act
            mowController.CheckAndAct();

            // Assert
            var daySummaryItems = logger.LogItems.Where(x => x.Type == LogType.DailyReport).ToList();

            Assert.AreEqual(1, daySummaryItems.Count);

            string expectedIntervalMowingTime = @"Total mowed: 5:59 hours.
                              ".Replace(" ", "").Replace("\r\n", " ");
            string expectedActualMowingTime = @"Actual out mowing time: 2:00 hours.
                              ".Replace(" ", "").Replace("\r\n", " ");

            string actualReport = daySummaryItems[0].Message.Replace(" ", "").Replace("\r\n", " ");

            Assert.IsTrue(actualReport.Contains(expectedIntervalMowingTime));
            Assert.IsTrue(actualReport.Contains(expectedActualMowingTime));
        }

        [TestMethod]
        public void MowingAFewDays_CreatingDailyReport_CorrectDailyReport()
        {
            // Arrange
            var logger = TestFactory.NewMowLogger(new DateTime(2019, 8, 8, 0, 0, 0));
            logger.LogItems.Add(new LogItem(new DateTime(2019, 8, 8, 0, 04, 0), LogType.PowerOn, LogLevel.Debug, ""));
            logger.LogItems.Add(new LogItem(new DateTime(2019, 8, 8, 11, 0, 0), LogType.MowingStarted, LogLevel.Debug, "")); // Just denna rad �r p�hittad
            logger.LogItems.Add(new LogItem(new DateTime(2019, 8, 8, 12, 0, 0), LogType.MowingEnded, LogLevel.Debug, ""));   // Just denna rad �r p�hittad
            logger.LogItems.Add(new LogItem(new DateTime(2019, 8, 8, 14, 34, 0), LogType.MowerLeft, LogLevel.Debug, ""));
            logger.LogItems.Add(new LogItem(new DateTime(2019, 8, 8, 14, 41, 0), LogType.MowerCame, LogLevel.Debug, ""));
            logger.LogItems.Add(new LogItem(new DateTime(2019, 8, 8, 19, 55, 0), LogType.PowerOff, LogLevel.Debug, ""));

            logger.LogItems.Add(new LogItem(new DateTime(2019, 8, 9, 0, 0, 0), LogType.NewDay, LogLevel.Debug, ""));
            logger.LogItems.Add(new LogItem(new DateTime(2019, 8, 9, 0, 04, 0), LogType.PowerOn, LogLevel.Debug, ""));
            logger.LogItems.Add(new LogItem(new DateTime(2019, 8, 9, 9, 55, 0), LogType.PowerOff, LogLevel.Debug, ""));
            logger.LogItems.Add(new LogItem(new DateTime(2019, 8, 9, 11, 31, 0), LogType.PowerOn, LogLevel.Debug, ""));
            logger.LogItems.Add(new LogItem(new DateTime(2019, 8, 9, 11, 31, 0), LogType.MowingStarted, LogLevel.Debug, ""));
            logger.LogItems.Add(new LogItem(new DateTime(2019, 8, 9, 11, 32, 0), LogType.MowerLeft, LogLevel.Debug, ""));
            logger.LogItems.Add(new LogItem(new DateTime(2019, 8, 9, 12, 0, 0), LogType.MowingEnded, LogLevel.Debug, ""));
            logger.LogItems.Add(new LogItem(new DateTime(2019, 8, 9, 13, 33, 0), LogType.MowerLost, LogLevel.Debug, ""));
            logger.LogItems.Add(new LogItem(new DateTime(2019, 8, 9, 14, 31, 0), LogType.MowerCame, LogLevel.Debug, ""));
            logger.LogItems.Add(new LogItem(new DateTime(2019, 8, 9, 15, 44, 0), LogType.MowerLeft, LogLevel.Debug, ""));
            logger.LogItems.Add(new LogItem(new DateTime(2019, 8, 9, 17, 44, 0), LogType.MowerLost, LogLevel.Debug, ""));
            logger.LogItems.Add(new LogItem(new DateTime(2019, 8, 9, 18, 38, 0), LogType.MowerCame, LogLevel.Debug, ""));
            logger.LogItems.Add(new LogItem(new DateTime(2019, 8, 9, 20, 0, 0), LogType.MowingStarted, LogLevel.Debug, ""));
            logger.LogItems.Add(new LogItem(new DateTime(2019, 8, 9, 20, 9, 0), LogType.MowerLeft, LogLevel.Debug, ""));
            logger.LogItems.Add(new LogItem(new DateTime(2019, 8, 9, 21, 41, 0), LogType.MowerCame, LogLevel.Debug, ""));
            logger.LogItems.Add(new LogItem(new DateTime(2019, 8, 9, 22, 34, 0), LogType.MowerLeft, LogLevel.Debug, ""));
            logger.LogItems.Add(new LogItem(new DateTime(2019, 8, 9, 23, 59, 0), LogType.MowingEnded, LogLevel.Debug, ""));

            var systemTime = new TestSystemTime(2019, 8, 10, 0, 0, 0);
            var config = TestFactory.NewConfig10To12And20To2359(usingContactHomeSensor: true);
            var powerSwitch = new TestPowerSwitch(PowerStatus.On);
            var weatherForecast = TestFactory.NewWeatherForecastGood(systemTime);
            var homeSensor = new TestHomeSensor(systemTime, isHome: true);
            var rainSensor = new TestRainSensor(isWet: false);
            var mowController = new MowController(config, powerSwitch, weatherForecast, systemTime, homeSensor, logger, rainSensor);

            // Act
            mowController.CheckAndAct();

            // Assert
            var daySummaryItems = logger.LogItems.Where(x => x.Type == LogType.DailyReport).ToList();

            Assert.AreEqual(1, daySummaryItems.Count);

            string expectedIntervalMowingTime = @"Total mowed: 4:28 hours.
                              ".Replace(" ", "").Replace("\r\n", " ");
            string expectedActualMowingTime = @"Actual out mowing time: 3:25 hours.
                              ".Replace(" ", "").Replace("\r\n", " ");
            string expectedMowerAwayTime = @"Exact mower away time: 8:50 hours.
                              ".Replace(" ", "").Replace("\r\n", " ");

            string actualReport = daySummaryItems[0].Message.Replace(" ", "").Replace("\r\n", " ");

            Assert.IsTrue(actualReport.Contains(expectedIntervalMowingTime));
            Assert.IsTrue(actualReport.Contains(expectedActualMowingTime));
            Assert.IsTrue(actualReport.Contains(expectedMowerAwayTime));
        }

        [TestMethod]
        public void MowingWithContactSensor_HasMowedAboveAverageInTheMiddleOfAnInterval_PowerOffAndMowingEnded()
        {
            // Arrange
            var systemTime = new TestSystemTime(2019, 8, 10, 22, 0, 0);
            var config = TestFactory.NewConfig08To11And20To23(
                usingContactHomeSensor: true,
                averageWorkPerDayHours: 4);

            var logger = TestFactory.NewMowLogger(new DateTime(2019, 8, 10, 0, 0, 0));
            logger.LogItems.Add(new LogItem(new DateTime(2019, 8, 10, 0, 0, 0), LogType.PowerOn, LogLevel.Debug, ""));
            logger.LogItems.Add(new LogItem(new DateTime(2019, 8, 10, 8, 0, 0), LogType.MowingStarted, LogLevel.Debug, ""));
            logger.LogItems.Add(new LogItem(new DateTime(2019, 8, 10, 8, 0, 0), LogType.MowerLeft, LogLevel.Debug, ""));
            logger.LogItems.Add(new LogItem(new DateTime(2019, 8, 10, 9, 30, 0), LogType.MowerCame, LogLevel.Debug, ""));
            logger.LogItems.Add(new LogItem(new DateTime(2019, 8, 10, 10, 30, 0), LogType.MowerLeft, LogLevel.Debug, ""));
            logger.LogItems.Add(new LogItem(new DateTime(2019, 8, 10, 11, 0, 0), LogType.MowingEnded, LogLevel.Debug, ""));
            logger.LogItems.Add(new LogItem(new DateTime(2019, 8, 10, 11, 05, 0), LogType.MowerCame, LogLevel.Debug, ""));

            logger.LogItems.Add(new LogItem(new DateTime(2019, 8, 10, 20, 0, 0), LogType.MowingStarted, LogLevel.Debug, ""));
            logger.LogItems.Add(new LogItem(new DateTime(2019, 8, 10, 20, 0, 0), LogType.MowerLeft, LogLevel.Debug, ""));
            logger.LogItems.Add(new LogItem(new DateTime(2019, 8, 10, 21, 30, 0), LogType.MowerCame, LogLevel.Debug, ""));

            var powerSwitch = new TestPowerSwitch(PowerStatus.On);
            var weatherForecast = TestFactory.NewWeatherForecastGood(systemTime);
            var homeSensor = new TestHomeSensor(systemTime, isHome: true);
            var rainSensor = new TestRainSensor(isWet: false);
            var mowController = new MowController(config, powerSwitch, weatherForecast, systemTime, homeSensor, logger, rainSensor);

            // Act
            mowController.CheckAndAct();

            // Assert
            Assert.AreEqual(powerSwitch.Status, PowerStatus.Off);

            var logItem = logger.LogItems.FirstOrDefault(x => x.Type == LogType.PowerOff);
            Assert.IsNotNull(logItem);
            Assert.AreEqual("Power was turned off. Mowing not necessary.", logItem.Message);

            logItem = logger.LogItems.FirstOrDefault(x => x.Type == LogType.MowingEnded && x.Time.Hour == 22);
            Assert.IsNotNull(logItem);
        }

        [TestMethod]
        public void MowingStartedYesterday_CreatingDailyReport_CorrectExaceMowerAwayTime()
        {
            // Arrange
            var systemTime = new TestSystemTime(2019, 8, 16, 0, 0, 0);
            var config = TestFactory.NewConfig10To12And20To2359(
                usingContactHomeSensor: true,
                averageWorkPerDayHours: 5
                );

            var logger = TestFactory.NewMowLogger(new DateTime(2019, 8, 15, 15, 0, 0));
            logger.LogItems.Add(new LogItem(new DateTime(2019, 8, 15, 15, 0, 0), LogType.PowerOn, LogLevel.Debug, ""));
            logger.LogItems.Add(new LogItem(new DateTime(2019, 8, 15, 16, 30, 0), LogType.MowerLeft, LogLevel.Debug, ""));
            logger.LogItems.Add(new LogItem(new DateTime(2019, 8, 15, 18, 0, 0), LogType.MowerCame, LogLevel.Debug, ""));
            logger.LogItems.Add(new LogItem(new DateTime(2019, 8, 15, 19, 0, 0), LogType.MowingStarted, LogLevel.Debug, ""));
            logger.LogItems.Add(new LogItem(new DateTime(2019, 8, 15, 19, 30, 0), LogType.MowerLeft, LogLevel.Debug, ""));
            logger.LogItems.Add(new LogItem(new DateTime(2019, 8, 15, 21, 0, 0), LogType.MowerCame, LogLevel.Debug, ""));
            logger.LogItems.Add(new LogItem(new DateTime(2019, 8, 15, 22, 0, 0), LogType.MowerLeft, LogLevel.Debug, ""));
            logger.LogItems.Add(new LogItem(new DateTime(2019, 8, 15, 23, 30, 0), LogType.MowerCame, LogLevel.Debug, ""));
            logger.LogItems.Add(new LogItem(new DateTime(2019, 8, 15, 23, 59, 0), LogType.MowingEnded, LogLevel.Debug, ""));
            logger.LogItems.Add(new LogItem(new DateTime(2019, 8, 16, 0, 0, 0), LogType.NewDay, LogLevel.Debug, ""));

            var powerSwitch = new TestPowerSwitch(PowerStatus.On);
            var weatherForecast = TestFactory.NewWeatherForecastGood(systemTime);
            var homeSensor = new TestHomeSensor(systemTime, isHome: true);
            var rainSensor = new TestRainSensor(isWet: false);
            var mowController = new MowController(config, powerSwitch, weatherForecast, systemTime, homeSensor, logger, rainSensor, true);

            // Act
            mowController.CheckAndAct();

            // Assert
            var daySummaryItems = logger.LogItems.Where(x => x.Type == LogType.DailyReport).ToList();

            Assert.AreEqual(1, daySummaryItems.Count);

            string expectedIntervalMowingTime = @"Total mowed: 4:59 hours.
                              ".Replace(" ", "").Replace("\r\n", " ");
            string expectedActualMowingTime = @"Actual out mowing time: 3:00 hours.
                              ".Replace(" ", "").Replace("\r\n", " ");
            string expectedMowerAwayTime = @"Exact mower away time: 4:30 hours.
                              ".Replace(" ", "").Replace("\r\n", " ");

            string actualReport = daySummaryItems[0].Message.Replace(" ", "").Replace("\r\n", " ");

            Assert.IsTrue(actualReport.Contains(expectedIntervalMowingTime));
            Assert.IsTrue(actualReport.Contains(expectedActualMowingTime));
            Assert.IsTrue(actualReport.Contains(expectedMowerAwayTime));
        }

        [TestMethod]
        public void MowingStartedYesterdayNotHomeFromStart_CreatingDailyReport_CorrectExaceMowerAwayTime()
        {
            // Arrange
            var systemTime = new TestSystemTime(2019, 8, 17, 0, 0, 0);
            var config = TestFactory.NewConfig06To10And19To2359(
                usingContactHomeSensor: true,
                averageWorkPerDayHours: 4
                );

            var logger = TestFactory.NewMowLogger(new DateTime(2019, 8, 16, 18, 0, 0));
            logger.LogItems.Add(new LogItem(new DateTime(2019, 8, 16, 18, 0, 0), LogType.PowerOn, LogLevel.Debug, ""));
            logger.LogItems.Add(new LogItem(new DateTime(2019, 8, 16, 18, 30, 0), LogType.MowerCame, LogLevel.Debug, ""));
            logger.LogItems.Add(new LogItem(new DateTime(2019, 8, 16, 18, 55, 0), LogType.PowerOff, LogLevel.Debug, ""));
            logger.LogItems.Add(new LogItem(new DateTime(2019, 8, 16, 22, 30, 0), LogType.MowerLeft, LogLevel.Debug, ""));

            var powerSwitch = new TestPowerSwitch(PowerStatus.On);
            var weatherForecast = TestFactory.NewWeatherForecastGood(systemTime);
            var homeSensor = new TestHomeSensor(systemTime, 
                isHome: false,
                mowerCameTime: new DateTime(2019, 8, 16, 18, 30, 0));
            var rainSensor = new TestRainSensor(isWet: false);
            var mowController = new MowController(config, powerSwitch, weatherForecast, systemTime, homeSensor, logger, rainSensor, false);

            // Act
            mowController.CheckAndAct();

            // Assert
            var daySummaryItems = logger.LogItems.Where(x => x.Type == LogType.DailyReport).ToList();

            Assert.AreEqual(1, daySummaryItems.Count);

            string expectedIntervalMowingTime = @"Total mowed: 0:00 hours.
                              ".Replace(" ", "").Replace("\r\n", " ");
            string expectedActualMowingTime = @"Actual out mowing time: 0:00 hours.
                              ".Replace(" ", "").Replace("\r\n", " ");
            string expectedMowerAwayTime = @"Exact mower away time: 1:59 hours.
                              ".Replace(" ", "").Replace("\r\n", " ");

            string actualReport = daySummaryItems[0].Message.Replace(" ", "").Replace("\r\n", " ");

            Assert.IsTrue(actualReport.Contains(expectedIntervalMowingTime));
            Assert.IsTrue(actualReport.Contains(expectedActualMowingTime));
            Assert.IsTrue(actualReport.Contains(expectedMowerAwayTime));
        }

        [TestMethod]
        public void MowingStartedTwoDaysAgo_CreatingDailyReport_CorrectExactMowerAwayTime()
        {
            // Arrange
            var systemTime = new TestSystemTime(2019, 8, 18, 0, 0, 0);
            var config = TestFactory.NewConfig06To10And19To2359(
                usingContactHomeSensor: true,
                averageWorkPerDayHours: 4
                );

            var logger = TestFactory.NewMowLogger(new DateTime(2019, 8, 16, 18, 10, 0));

            logger.LogItems.Add(new LogItem(new DateTime(2019, 8, 16, 22, 30, 0), LogType.MowerLeft, LogLevel.Debug, ""));

            logger.LogItems.Add(new LogItem(new DateTime(2019, 8, 17, 0, 0, 0), LogType.NewDay, LogLevel.Debug, ""));
            logger.LogItems.Add(new LogItem(new DateTime(2019, 8, 17, 0, 4, 0), LogType.PowerOn, LogLevel.Debug, ""));
            logger.LogItems.Add(new LogItem(new DateTime(2019, 8, 17, 0, 30, 0), LogType.MowerLost, LogLevel.Debug, ""));
            logger.LogItems.Add(new LogItem(new DateTime(2019, 8, 17, 8, 24, 0), LogType.MowerCame, LogLevel.Debug, ""));
            logger.LogItems.Add(new LogItem(new DateTime(2019, 8, 17, 8, 24, 0), LogType.MowingStarted, LogLevel.Debug, ""));
            logger.LogItems.Add(new LogItem(new DateTime(2019, 8, 17, 8, 54, 0), LogType.PowerOff, LogLevel.Debug, ""));
            logger.LogItems.Add(new LogItem(new DateTime(2019, 8, 17, 8, 54, 0), LogType.MowingEnded, LogLevel.Debug, ""));
            logger.LogItems.Add(new LogItem(new DateTime(2019, 8, 17, 10, 5, 0), LogType.PowerOn, LogLevel.Debug, ""));
            logger.LogItems.Add(new LogItem(new DateTime(2019, 8, 17, 18, 55, 0), LogType.PowerOff, LogLevel.Debug, ""));

            var powerSwitch = new TestPowerSwitch(PowerStatus.On);
            var weatherForecast = TestFactory.NewWeatherForecastBad(systemTime);
            var homeSensor = new TestHomeSensor(systemTime, isHome: true);
            var rainSensor = new TestRainSensor(isWet: true);
            var mowController = new MowController(config, powerSwitch, weatherForecast, systemTime, homeSensor, logger, rainSensor, false);

            // Act
            mowController.CheckAndAct();

            // Assert
            var daySummaryItems = logger.LogItems.Where(x => x.Type == LogType.DailyReport).ToList();

            Assert.AreEqual(1, daySummaryItems.Count);

            string expectedIntervalMowingTime = @"Total mowed: 0:30 hours.
                              ".Replace(" ", "").Replace("\r\n", " ");
            string expectedActualMowingTime = @"Actual out mowing time: 0:00 hours.
                              ".Replace(" ", "").Replace("\r\n", " ");
            string expectedMowerAwayTime = @"Exact mower away time: 8:24 hours.
                              ".Replace(" ", "").Replace("\r\n", " ");

            string actualReport = daySummaryItems[0].Message.Replace(" ", "").Replace("\r\n", " ");

            Assert.IsTrue(actualReport.Contains(expectedIntervalMowingTime));
            Assert.IsTrue(actualReport.Contains(expectedActualMowingTime));
            Assert.IsTrue(actualReport.Contains(expectedMowerAwayTime));
        }

        [TestMethod]
        public void MowingStartedTwoDaysAgo_MissingMowerComesHomeInWet_NotMowingStarted()
        {
            // Arrange
            var systemTime = new TestSystemTime(2019, 8, 18, 8, 24, 0);
            var config = TestFactory.NewConfig06To10And19To2359(
                usingContactHomeSensor: true,
                averageWorkPerDayHours: 4
                );

            var logger = TestFactory.NewMowLogger(new DateTime(2019, 8, 16, 18, 10, 0));

            logger.LogItems.Add(new LogItem(new DateTime(2019, 8, 16, 22, 30, 0), LogType.MowerLeft, LogLevel.Debug, ""));

            logger.LogItems.Add(new LogItem(new DateTime(2019, 8, 17, 0, 0, 0), LogType.NewDay, LogLevel.Debug, ""));
            logger.LogItems.Add(new LogItem(new DateTime(2019, 8, 17, 0, 4, 0), LogType.PowerOn, LogLevel.Debug, ""));
            logger.LogItems.Add(new LogItem(new DateTime(2019, 8, 17, 0, 30, 0), LogType.MowerLost, LogLevel.Debug, ""));

            var powerSwitch = new TestPowerSwitch(PowerStatus.On);
            var weatherForecast = TestFactory.NewWeatherForecastBad(systemTime);
            var homeSensor = new TestHomeSensor(systemTime, 
                isHome: true, 
                mowerLeftTime: new DateTime(2019, 8, 16, 22, 30, 0), 
                mowerCameTime: new DateTime(2019, 8, 18, 8, 24, 0));
            var rainSensor = new TestRainSensor(isWet: true);
            var mowController = new MowController(config, powerSwitch, weatherForecast, systemTime, homeSensor, logger, rainSensor, false);

            // Act
            mowController.CheckAndAct();

            // Assert
            Assert.AreEqual(powerSwitch.Status, PowerStatus.On);

            var logItem = logger.LogItems.FirstOrDefault(x => x.Type == LogType.MowingStarted);
            Assert.IsNull(logItem);
        }
    }
}
