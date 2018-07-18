﻿using MowControl;
using System;
using System.Collections.Generic;
using System.Text;

namespace MowerTests
{
    public static class TestFactory
    {
        public static IMowPlannerConfig NewConfig6To12()
        {
            var timeIntervals = new List<TimeInterval>();
            timeIntervals.Add(new TimeInterval(6, 0, 12, 0));

            return new MowPlannerConfig()
            {
                TimeIntervals = timeIntervals,
                AverageWorkPerDayHours = 12,
                MaxHourlyThunderPercent = 0,
                MaxHourlyPrecipitaionMillimeter = 0
            };

        }

        public static IMowPlannerConfig NewConfig6To12And13To19()
        {
            var timeIntervals = new List<TimeInterval>();
            timeIntervals.Add(new TimeInterval(6, 0, 12, 0));
            timeIntervals.Add(new TimeInterval(13, 0, 19, 0));

            return new MowPlannerConfig()
            {
                TimeIntervals = timeIntervals,
                AverageWorkPerDayHours = 8,
                MaxHourlyThunderPercent = 0,
                MaxHourlyPrecipitaionMillimeter = 0
            };
        }

        public static IMowPlannerConfig NewConfig6To12And18To2359()
        {
            var timeIntervals = new List<TimeInterval>();
            timeIntervals.Add(new TimeInterval(6, 0, 12, 0));
            timeIntervals.Add(new TimeInterval(18, 0, 23, 59));

            return new MowPlannerConfig()
            {
                TimeIntervals = timeIntervals,
                AverageWorkPerDayHours = 8,
                MaxHourlyThunderPercent = 0,
                MaxHourlyPrecipitaionMillimeter = 0
            };
        }

        public static IMowPlannerConfig NewConfig0To6And12To18()
        {
            var timeIntervals = new List<TimeInterval>();
            timeIntervals.Add(new TimeInterval(0, 0, 6, 0));
            timeIntervals.Add(new TimeInterval(12, 0, 18, 0));

            return new MowPlannerConfig()
            {
                TimeIntervals = timeIntervals,
                AverageWorkPerDayHours = 8,
                MaxHourlyThunderPercent = 0,
                MaxHourlyPrecipitaionMillimeter = 0
            };
        }

        public static IMowPlannerConfig NewConfig3To10And16To2300()
        {
            var timeIntervals = new List<TimeInterval>();
            timeIntervals.Add(new TimeInterval(3, 0, 10, 0));
            timeIntervals.Add(new TimeInterval(16, 0, 23, 00));

            return new MowPlannerConfig()
            {
                TimeIntervals = timeIntervals,
                AverageWorkPerDayHours = 10,
                MaxHourlyThunderPercent = 0,
                MaxHourlyPrecipitaionMillimeter = 0
            };
        }

        public static TestWeatherForecast NewWeatherForecastGood(SystemTime time)
        {
            return new TestWeatherForecast(expectingGoodWeather: true, systemTime: time);
        }

        public static TestWeatherForecast NewWeatherForecastGood(ISystemTime systemTime)
        {
            return new TestWeatherForecast(true, systemTime);
        }

        public static TestWeatherForecast NewWeatherForecastBad(ISystemTime systemTime)
        {
            return new TestWeatherForecast(false, systemTime);
        }

        public static IMowLogger NewMowLogger(int startItemDaysAgo = 0)
        {
            var logger = new MowLogger();
            DateTime date = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day);
            date = date.AddDays(-startItemDaysAgo);

            logger.Write(date, LogType.MowControllerStarted, "Mow controller started.");

            return logger;
        }

        public static IMowLogger NewMowLogger(DateTime startItemDate)
        {
            var logger = new MowLogger();
            logger.Write(startItemDate, LogType.MowControllerStarted, "Mow controller started.");
            return logger;
        }
    }
}