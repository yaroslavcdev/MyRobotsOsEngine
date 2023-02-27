using System.Collections.Generic;
using System.IO;
using OsEngine.Entity;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Tab;

namespace OsEngine.Robots
{
    public class MyRobot : BotPanel
    {
        public MyRobot(string name, StartProgram startProgram) : base(name, startProgram)
        {
            this.TabCreate(BotTabType.Simple);

            _tab = TabsSimple[0];

            this.CreateParameter("Mode", "Edit", new[] { "Edit", "Trade" });

            _risk = CreateParameter("Risk %", 1m, 0.1m, 10m, 0.1m);

            _profitKoef = CreateParameter("Koef Profit", 1m, 0.1m, 10m, 0.1m);

            _countDownCandles = CreateParameter("Count down candles", 1, 1, 5, 1);

            _koefVolume = CreateParameter("Koef volume", 1.5m, 2m, 10m, 0.5m);

            _countCandles = CreateParameter("Count candles", 10, 5, 50, 1);
            
            _tab.CandleFinishedEvent += TabOnCandleFinishedEvent;
            
            _tab.PositionOpeningSuccesEvent += TabOnPositionOpeningSuccesEvent;
            
            _tab.PositionClosingSuccesEvent += TabOnPositionClosingSuccesEvent;
        }

        #region Fields

        private BotTabSimple _tab;

        /// Риск на сделку
        private StrategyParameterDecimal _risk;
        
        /// Во сколько раз тейк больше риска
        private StrategyParameterDecimal _profitKoef;

        /// Количество падающих свечей перед объёмным разворотом
        private StrategyParameterInt _countDownCandles;

        /// Во сколько раз объём превышает средний
        private StrategyParameterDecimal _koefVolume;
        
        /// Средний объём
        private decimal _averageVolume;

        /// Количество пунктов до стоп лосса
        private int _punkts = 0;

        private decimal _lowCandle = 0;

        /// Количество свечей для вычисления среднего объёма
        private StrategyParameterInt _countCandles;

        #endregion

        #region Methods

        private void TabOnCandleFinishedEvent(List<Candle> candles)
        {
            if (candles.Count < _countDownCandles.ValueInt + 1 || candles.Count < _countCandles.ValueInt + 1)
            {
                return;
            }

            _averageVolume = 0;

            for (int i = candles.Count - 2; i > candles.Count - _countCandles.ValueInt - 2; i--)
            {
                _averageVolume += candles[i].Volume;
            }

            _averageVolume /= _countCandles.ValueInt;

            List<Position> positions = _tab.PositionOpenLong;

            if (positions.Count > 0)
            {
                return;
            }

            Candle candle = candles[candles.Count - 1];

            if (candle.Close < (candle.High + candle.Low) / 2 || candle.Volume < _averageVolume * _koefVolume.ValueDecimal)
            {
                return;
            }

            for (int i = candles.Count - 2; i > candles.Count - 2 - _countDownCandles.ValueInt; i--)
            {
                if (candles[i].Close > candles[i].Open)
                {
                    return;
                }
            }
            
            _punkts = (int)((candle.Close - candle.Low) / _tab.Securiti.PriceStep);

            if (_punkts < 5)
            {
                return;
            }

            decimal amountStop = _punkts * _tab.Securiti.PriceStepCost; // 1

            decimal amountRisk = _tab.Portfolio.ValueBegin * _risk.ValueDecimal / 100;
            
            decimal volume = amountRisk / amountStop;

            decimal go = 10; // 1

            if (_tab.Securiti.Go > 1)
            {
                go = _tab.Securiti.Go;
            }

            decimal maxLot = _tab.Portfolio.ValueBegin / go;

            if (volume < maxLot)
            {
                _lowCandle = candle.Low;
                
                _tab.BuyAtMarket(volume);
            }
            
        }
        
        private void TabOnPositionOpeningSuccesEvent(Position pos)
        {
            decimal priceTake = pos.EntryPrice + _punkts * _profitKoef.ValueDecimal;
            
            _tab.CloseAtProfit(pos, priceTake, priceTake);
            
            _tab.CloseAtStop(pos, _lowCandle, _lowCandle - 100 * _tab.Securiti.PriceStep);
        }
        
        private void TabOnPositionClosingSuccesEvent(Position pos)
        {
            SaveSCV(pos);
        }

        private void SaveSCV(Position pos)
        {
            if (File.Exists(@"Engine\trades.csv"))
            {
                string header = ";Позиция;Символ;Лоты;Изменение/Максимум Лотов;" +
                                "Исполнение входа;Сигнал входа;Бар входа;Дата входа;" +
                                "Время входа;Цена входа;Комиссия входа;Исполнение выхода;" +
                                "Сигнал выхода;Бар выхода;Дата выхода;Время выхода;Цена выхода;" +
                                "Комиссия выхода;Средневзвешенная цена входа;П/У;П/У сделки;П/У с одного лота;" +
                                "Зафиксированная П/У;Открытая П/У;Продолж. (баров);Доход/Бар;Общий П/У;% изменения;" +
                                "MAE;MAE %;MFE;MFE %";
                
                using (StreamWriter writer = new StreamWriter(@"Engine\trades.csv", false))
                {
                    writer.WriteLine(header);
                    
                    writer.Close();
                }
            }
            
            using (StreamWriter writer = new StreamWriter(@"Engine\trades.csv", true))
            {
                string str = ";;;;;;;;" + pos.TimeOpen.ToShortDateString();
                str += ";" + pos.TimeOpen.TimeOfDay;
                str += ";;;;;;;;;;;;;;" + pos.ProfitPortfolioPunkt + ";;;;;;;;;";
                
                writer.WriteLine(str);
                    
                writer.Close();
            }
        }
        
        public override string GetNameStrategyType()
        {
            return nameof(MyRobot);
        }

        public override void ShowIndividualSettingsDialog()
        {
            
        }

        #endregion
        
    }
}