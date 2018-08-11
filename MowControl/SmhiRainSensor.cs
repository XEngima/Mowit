﻿using SmhiWeather;
using System;
using System.Collections.Generic;
using System.Text;

namespace MowControl
{
    public class SmhiRainSensor : IRainSensor
    {
        private ISmhi _smhi;
        private DateTime _lastRainTime;
        private decimal _lastRainIntensity;

        public SmhiRainSensor(ISmhi smhi)
        {
            _smhi = smhi;
            _lastRainTime = DateTime.MinValue;
            _lastRainIntensity = 0;
        }

        public bool IsWet
        {
            get { return false; }
        }
    }
}