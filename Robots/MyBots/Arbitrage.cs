using System;
using System.Collections.Generic;
using OsEngine.Charts.CandleChart.Indicators;
using OsEngine.Entity;
using OsEngine.Market;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Tab;

namespace OsEngine.Robots.MyBots
{
    public class ArbitrageBot : BotPanel
    {
        private readonly Atr _atr;

        // переменная состояния
        private readonly StrategyParameterBool _isOn;

        // длина atr
        private readonly StrategyParameterInt _lengthAtr;

        // параметры 
        // длина скользящей средней
        private readonly StrategyParameterInt _lengthMa;

        // индикаторы
        private readonly MovingAverage _ma;

        // мультипликатор для принятия решения о входе
        private readonly StrategyParameterDecimal _multiplier;
        private readonly BotTabIndex _tabIndex;

        // шаг 2
        // для торговли нужны 3 вкладки
        // 2 для торговли, 1 для индекса
        private readonly BotTabSimple _tabToTrade1;

        private readonly BotTabSimple _tabToTrade2;

        // объем входа
        private readonly StrategyParameterDecimal _volume1;
        private readonly StrategyParameterDecimal _volume2;

        public ArbitrageBot(string name, StartProgram startProgram) : base(name, startProgram)
        {
            // шаг 3
            // инициализация вкладок
            TabCreate(BotTabType.Simple);
            TabCreate(BotTabType.Simple);
            TabCreate(BotTabType.Index);

            // присвоение значений 
            _tabToTrade1 = TabsSimple[0];
            _tabToTrade2 = TabsSimple[1];
            _tabIndex = TabsIndex[0];

            // шаг 4
            // инициализация индикаторов

            // скользящая средняя
            // false - чтобы пользователь не удалил индикатор на графике вручную, иначе ошибки
            _ma = new MovingAverage(name + "MA", false);
            // передаём индикатор на прорисовку во вкладку с индексом
            // Prime - индикатор будет лежать в главной области рядом со свечами
            _ma = (MovingAverage) _tabIndex.CreateCandleIndicator(_ma, "Prime");
            _ma.Save();

            // передаём индикатор на прорисовку во вкладку с индексом
            // Second - индикатор будет лежать на следующей вкладке после Prime
            _atr = new Atr(name + "Atr", false);
            _atr = (Atr) _tabIndex.CreateCandleIndicator(_atr, "Second");
            _atr.Save();

            // шаг 5
            // инициализация параметров 
            _isOn = CreateParameter("IsOn", false);
            _lengthMa = CreateParameter("Length MA", 20, 20, 100, 5);
            _lengthAtr = CreateParameter("Length Atr", 20, 20, 100, 5);
            _volume1 = CreateParameter("Volume1", 0.1m, 0.1m, 5, 0.1m);
            _volume2 = CreateParameter("Volume2", 0.1m, 0.1m, 5, 0.1m);
            _multiplier = CreateParameter("Multiplier", 1, 1, 5, 0.5m);

            // подписка на событие завершения свечи
            _tabIndex.SpreadChangeEvent += _tabIndex_SpreadChangeEvent;

            // шаг 7
            // меняем настройки для индикаторов
            ParametrsChangeByUser += ArbitrageBot_ParametrsChangeByUser;
        }

        private void ArbitrageBot_ParametrsChangeByUser()
        {
            _ma.Lenght = _lengthMa.ValueInt;
            _atr.Lenght = _lengthAtr.ValueInt;
        }


        public override string GetNameStrategyType()
        {
            // шаг 1
            //создаем имя робота и добавляем его в BotPanel.cs
            return "Arbitrage";
        }

        public override void ShowIndividualSettingsDialog()
        {
            throw new NotImplementedException();
        }


        // шаг 6
        // логика торговли 
        private void _tabIndex_SpreadChangeEvent(List<Candle> candles)
        {
            if (_isOn.ValueBool == false) return;

            // 1 если свечей меньше чем нужно индикаторам - выходим
            // 2 если в одной из вкладок нет данных - выходим
            // 3 если есть позиция лонг, то смотрим выход из лонга
            // 4 если есть позиция шорт, то смотрим выход из шорта
            // 5 если позиций нет, то смотрим вход

            // проверка двух условий
            if (candles.Count < _atr.Lenght ||
                candles.Count < _ma.Lenght ||
                _tabToTrade1.IsConnected == false ||
                _tabToTrade2.IsConnected == false)
                return;

            // список позиций
            var positions = _tabToTrade1.PositionsOpenAll;

            // если позиций нет
            if (positions.Count == 0)
            {
                // берем последнее значение ma и прибавляем последнее значение atr
                // если это значение меньше, чем текущая цена на индексе, то
                // входим в лонг в tab1, и шорт по tab2
                if (_ma.Values[_ma.Values.Count - 1] +
                    _atr.Values[_atr.Values.Count - 1] *
                    _multiplier.ValueDecimal <
                    candles[candles.Count - 1].Close)
                {
                    //long at tab1, short at tab2
                    _tabToTrade1.BuyAtMarket(_volume1.ValueDecimal);
                    _tabToTrade2.SellAtMarket(_volume2.ValueDecimal);
                }

                // если наоборот, то
                // входим шорт по tab1, и в лонг в tab2
                if (_ma.Values[_ma.Values.Count - 1] -
                    _atr.Values[_atr.Values.Count - 1] *
                    _multiplier.ValueDecimal >
                    candles[candles.Count - 1].Close)
                {
                    // short at tab1, long at tab2
                    _tabToTrade1.SellAtMarket(_volume1.ValueDecimal);
                    _tabToTrade2.BuyAtMarket(_volume2.ValueDecimal);
                }
            }
            // если позиция buy
            else if (positions[0].Direction == Side.Buy)
            {
                if (positions[0].State != PositionStateType.Open) return;

                // если выполнятеся условие, что необходимо открыть позицию шорт,
                // то закрываем позицию лонг
                if (_ma.Values[_ma.Values.Count - 1] -
                    _atr.Values[_atr.Values.Count - 1] * _multiplier.ValueDecimal >
                    candles[candles.Count - 1].Close)
                {
                    _tabToTrade1.CloseAllAtMarket();
                    _tabToTrade2.CloseAllAtMarket();
                }
            }
            //если позиция sell
            else if (positions[0].Direction == Side.Sell)
            {
                if (positions[0].State != PositionStateType.Open) return;
                // если выполнятеся условие, что необходимо открыть позицию лонг,
                // то закрываем позицию шорт
                if (_ma.Values[_ma.Values.Count - 1] +
                    _atr.Values[_atr.Values.Count - 1] * _multiplier.ValueDecimal <
                    candles[candles.Count - 1].Close)
                {
                    _tabToTrade1.CloseAllAtMarket();
                    _tabToTrade2.CloseAllAtMarket();
                }
            }
        }
    }
}