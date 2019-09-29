using System;
using System.Collections.Generic;
using OsEngine.Charts.CandleChart.Indicators;
using OsEngine.Entity;
using OsEngine.Market;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Tab;

namespace OsEngine.Robots.MyBots
{
    internal class MomentumMtStopLoss : BotPanel
    {
        // параметры
        private readonly StrategyParameterBool _isOn;
        private readonly StrategyParameterInt _length_mom_less;
        private readonly StrategyParameterInt _length_mom_more;
        private readonly Momentum _momentum_less;

        // индикаторы Моментума
        private readonly Momentum _momentum_more;

        private readonly Candle _stopLoss = new Candle();

        // вкладка
        private readonly BotTabSimple _tabToTrade;
        private readonly StrategyParameterInt _volume;

        public MomentumMtStopLoss(string name, StartProgram startProgram) : base(name, startProgram)
        {
            // вкладка для торговли
            TabCreate(BotTabType.Simple);
            _tabToTrade = TabsSimple[0];

            // индикаторы моментума
            _momentum_more = new Momentum(name + "_momentum_more", false);
            _momentum_more = (Momentum) _tabToTrade.CreateCandleIndicator(_momentum_more, "Momentum");
            _momentum_more.Save();

            _momentum_less = new Momentum(name + "_momentum_less", false);
            _momentum_less = (Momentum) _tabToTrade.CreateCandleIndicator(_momentum_less, "Momentum");
            _momentum_less.Save();

            //параметры для моментума 
            _isOn = CreateParameter("IsOn", false);
            _volume = CreateParameter("Volume", 1000, 1000, 8000, 1000);
            _length_mom_more = CreateParameter("Length  Momentum_More", 60, 10, 60, 10);
            _length_mom_less = CreateParameter("Length  Momentum_Less", 30, 10, 30, 10);

            _tabToTrade.CandleFinishedEvent += _tabToTrade_CandleFinishedEvent;

            ParametrsChangeByUser += MomentumMultiTimeframe_ParametrsChangeByUser;

            _stopLoss.High = 0;
            _stopLoss.Low = 1000000;
        }

        private void MomentumMultiTimeframe_ParametrsChangeByUser()
        {
            _momentum_more.Nperiod = _length_mom_more.ValueInt;
            _momentum_less.Nperiod = _length_mom_less.ValueInt;
        }

        public override string GetNameStrategyType()
        {
            return "MMT_StopLoss";
        }

        public override void ShowIndividualSettingsDialog()
        {
            throw new NotImplementedException();
        }

        //логика торговли
        private void _tabToTrade_CandleFinishedEvent(List<Candle> candles)
        {
            if (_isOn.ValueBool == false) return;

            if (_momentum_more.Nperiod < _momentum_less.Nperiod) return;

            if (candles.Count < _length_mom_more.ValueInt || _tabToTrade.IsConnected == false) return;

            var directionUp = _momentum_less.Values[_momentum_less.Values.Count - 2] >
                              _momentum_less.Values[_momentum_less.Values.Count - 3] &&
                              _momentum_more.Values[_momentum_more.Values.Count - 2] >
                              _momentum_more.Values[_momentum_more.Values.Count - 3] &&
                              _momentum_less.Values[_momentum_less.Values.Count - 1] >
                              _momentum_less.Values[_momentum_less.Values.Count - 2] &&
                              _momentum_more.Values[_momentum_more.Values.Count - 1] >
                              _momentum_more.Values[_momentum_more.Values.Count - 2];

            var directionDown = _momentum_less.Values[_momentum_less.Values.Count - 2] <
                                _momentum_less.Values[_momentum_less.Values.Count - 3] &&
                                _momentum_more.Values[_momentum_more.Values.Count - 2] <
                                _momentum_more.Values[_momentum_more.Values.Count - 3] &&
                                _momentum_less.Values[_momentum_less.Values.Count - 1] <
                                _momentum_less.Values[_momentum_less.Values.Count - 2] &&
                                _momentum_more.Values[_momentum_more.Values.Count - 1] <
                                _momentum_more.Values[_momentum_more.Values.Count - 2];


            var positions = _tabToTrade.PositionsOpenAll;
            var _lastCandle = candles[candles.Count - 1];

            if (positions.Count == 0)
            {
                if (directionUp)
                    if (candles[candles.Count - 1].High > candles[candles.Count - 2].High)
                    {
                        _tabToTrade.BuyAtMarket(_volume.ValueInt);
                        _stopLoss.Low = _lastCandle.Low;
                    }

                if (directionDown)
                    if (candles[candles.Count - 1].Low > candles[candles.Count - 2].Low)
                    {
                        _tabToTrade.SellAtMarket(_volume.ValueInt);
                        _stopLoss.High = _lastCandle.High;
                    }
            }

            else if (positions[0].Direction == Side.Buy)
            {
                if (positions[0].State != PositionStateType.Open) return;

                if (directionDown || _lastCandle.Low >= _stopLoss.Low) _tabToTrade.CloseAllAtMarket();
            }

            else if (positions[0].Direction == Side.Sell)
            {
                if (positions[0].State != PositionStateType.Open) return;

                if (directionUp || _lastCandle.High >= _stopLoss.High) _tabToTrade.CloseAllAtMarket();
            }
        }
    }
}