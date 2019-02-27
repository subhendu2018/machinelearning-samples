﻿using Microsoft.ML;
using System;
using System.IO;
using System.Linq;
using static eShopForecastModelsTrainer.ConsoleHelpers;
using Common;
using Microsoft.ML.Data;

namespace eShopForecastModelsTrainer
{
    public class ProductModelHelper
    {
        private static string NumFeatures = nameof(NumFeatures);

        private static string CatFeatures = nameof(CatFeatures);

        /// <summary>
        /// Train and save model for predicting next month country unit sales
        /// </summary>
        /// <param name="dataPath">Input training file path</param>
        /// <param name="outputModelPath">Trained model path</param>
        public static void TrainAndSaveModel(MLContext mlContext, string dataPath, string outputModelPath = "product_month_fastTreeTweedie.zip")
        {
            if (File.Exists(outputModelPath))
            {
                File.Delete(outputModelPath);
            }

            CreateProductModelUsingPipeline(mlContext, dataPath, outputModelPath);
        }


        /// <summary>
        /// Build model for predicting next month country unit sales using Learning Pipelines API
        /// </summary>
        /// <param name="dataPath">Input training file path</param>
        /// <returns></returns>
        private static void CreateProductModelUsingPipeline(MLContext mlContext, string dataPath, string outputModelPath)
        {
            ConsoleWriteHeader("Training product forecasting");

            var trainingDataView = mlContext.Data.ReadFromTextFile<ProductData>(dataPath, hasHeader: true, separatorChar:',');

            var trainer = mlContext.Regression.Trainers.FastTreeTweedie(labelColumnName: DefaultColumnNames.Label, featureColumnName: DefaultColumnNames.Features);

            var trainingPipeline = mlContext.Transforms.Concatenate(outputColumnName: NumFeatures, nameof(ProductData.year), nameof(ProductData.month), nameof(ProductData.units), nameof(ProductData.avg), nameof(ProductData.count), 
                nameof(ProductData.max), nameof(ProductData.min), nameof(ProductData.prev) )
                .Append(mlContext.Transforms.Categorical.OneHotEncoding(outputColumnName: CatFeatures, inputColumnName: nameof(ProductData.productId)))
                .Append(mlContext.Transforms.Concatenate(outputColumnName: DefaultColumnNames.Features, NumFeatures, CatFeatures))
                .Append(mlContext.Transforms.CopyColumns(outputColumnName: DefaultColumnNames.Label, inputColumnName: nameof(ProductData.next)))
                .Append(trainer);
            
            // Cross-Validate with single dataset (since we don't have two datasets, one for training and for evaluate)
            // in order to evaluate and get the model's accuracy metrics
            Console.WriteLine("=============== Cross-validating to get model's accuracy metrics ===============");
            var crossValidationResults = mlContext.Regression.CrossValidate(data:trainingDataView, estimator:trainingPipeline, numFolds: 6, labelColumn: DefaultColumnNames.Label);
            ConsoleHelper.PrintRegressionFoldsAverageMetrics(trainer.ToString(), crossValidationResults);

            // Train the model
            var model = trainingPipeline.Fit(trainingDataView);

            // Save the model for later comsumption from end-user apps
            using (var file = File.OpenWrite(outputModelPath))
                model.SaveTo(mlContext, file);
        }

        /// <summary>
        /// Predict samples using saved model
        /// </summary>
        /// <param name="outputModelPath">Model file path</param>
        /// <returns></returns>
        public static void TestPrediction(MLContext mlContext, string outputModelPath = "product_month_fastTreeTweedie.zip")
        {
            ConsoleWriteHeader("Testing Product Unit Sales Forecast model");

            // Read the model that has been previously saved by the method SaveModel

            ITransformer trainedModel;
            using (var stream = File.OpenRead(outputModelPath))
            {
                trainedModel = mlContext.Model.Load(stream);
            }

            var predictionEngine = trainedModel.CreatePredictionEngine<ProductData, ProductUnitPrediction>(mlContext);

            Console.WriteLine("** Testing Product 1 **");

            // Build sample data
            ProductData dataSample = new ProductData()
            {
                productId = "263",
                month = 10,
                year = 2017,
                avg = 91,
                max = 370,
                min = 1,
                count = 10,
                prev = 1675,
                units = 910
            };

            // Predict the nextperiod/month forecast to the one provided
            ProductUnitPrediction prediction = predictionEngine.Predict(dataSample);
            Console.WriteLine($"Product: {dataSample.productId}, month: {dataSample.month + 1}, year: {dataSample.year} - Real value (units): 551, Forecast Prediction (units): {prediction.Score}");

            dataSample = new ProductData()
            {
                productId = "263",
                month = 11,
                year = 2017,
                avg = 29,
                max = 221,
                min = 1,
                count = 35,
                prev = 910,
                units = 551
            };

            // Predicts the nextperiod/month forecast to the one provided
            prediction = predictionEngine.Predict(dataSample);
            Console.WriteLine($"Product: {dataSample.productId}, month: {dataSample.month + 1}, year: {dataSample.year} - Forecast Prediction (units): {prediction.Score}");

            Console.WriteLine(" ");

            Console.WriteLine("** Testing Product 2 **");

            dataSample = new ProductData()
            {
                productId = "988",
                month = 10,
                year = 2017,
                avg = 43,
                max = 220,
                min = 1,
                count = 25,
                prev = 1036,
                units = 1094
            };

            prediction = predictionEngine.Predict(dataSample);
            Console.WriteLine($"Product: {dataSample.productId}, month: {dataSample.month + 1}, year: {dataSample.year} - Real Value (units): 1076, Forecasting (units): {prediction.Score}");

            dataSample = new ProductData()
            {
                productId = "988",
                month = 11,
                year = 2017,
                avg = 41,
                max = 225,
                min = 4,
                count = 26,
                prev = 1094,
                units = 1076
            };

            prediction = predictionEngine.Predict(dataSample);
            Console.WriteLine($"Product: {dataSample.productId}, month: {dataSample.month + 1}, year: {dataSample.year} - Forecasting (units): {prediction.Score}");
        }
    }
}
