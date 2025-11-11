// 파일: WorkPartner/PredictionService.cs
using Microsoft.ML;
using Microsoft.ML.Data;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Microsoft.ML.Data; // 👈 [추가]
using System.Linq; // 👈 [추가]

namespace WorkPartner.AI
{
    public class PredictionService
    {
        private readonly MLContext _mlContext;
        private ITransformer _model;
        private PredictionEngine<ModelInput, ModelOutput> _predictionEngine;

        public PredictionService()
        {
            _mlContext = new MLContext(seed: 0);
            LoadModel(); // 👈 [수정] 메서드 이름 변경 (생성자 분리)
        }

        // ▼▼▼ [수정] 기존 LoadModelAndCreateEngine 메서드를 이렇게 변경 ▼▼▼
        private void LoadModel()
        {
            try
            {
                // 1. 사용자가 훈련시킨 모델(user_model.zip)이 있는지 확인
                if (File.Exists(DataManager.UserModelFilePath))
                {
                    Debug.WriteLine("Loading User-Trained Model...");
                    _model = _mlContext.Model.Load(DataManager.UserModelFilePath, out _);
                }
                // 2. 없으면, 기본 샘플 모델(model_input.json)을 로드
                else if (File.Exists(DataManager.ModelFilePath))
                {
                    Debug.WriteLine("Loading Default Sample Model...");
                    var sampleData = LoadSampleData(DataManager.ModelFilePath);
                    var pipeline = BuildTrainingPipeline();
                    _model = pipeline.Fit(sampleData);
                }
                else
                {
                    Debug.WriteLine("No model file found.");
                    return;
                }

                _predictionEngine = _mlContext.Model.CreatePredictionEngine<ModelInput, ModelOutput>(_model);
                Debug.WriteLine("Prediction engine created successfully.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading model: {ex.Message}");
            }
        }
        // ▲▲▲ [수정 완료] ▲▲▲

        // (기존 LoadSampleData 메서드 - 수정 없음)
        private IDataView LoadSampleData(string filePath)
        {
            try
            {
                var dataView = _mlContext.Data.CreateTextLoader<ModelInput>(
                    separatorChar: ',',
                    hasHeader: true,
                    allowQuoting: false)
                    .Load(filePath);
                return dataView;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading sample data: {ex.Message}");
                return null;
            }
        }

        // ▼▼▼ [수정] 이 메서드의 이름을 BuildTrainingPipeline으로 변경하고,
        //          'FocusScore'를 'Label'로 복사하는 라인을 추가합니다.
        private IEstimator<ITransformer> BuildTrainingPipeline()
        {
            // 1. 데이터를 변환하는 파이프라인 정의
            var dataProcessPipeline = _mlContext.Transforms.Conversion.MapValueToKey(inputColumnName: nameof(ModelInput.TaskName), outputColumnName: "TaskNameFeaturized")
                .Append(_mlContext.Transforms.Categorical.OneHotEncoding(inputColumnName: "TaskNameFeaturized", outputColumnName: "TaskNameEncoded"))
                .Append(_mlContext.Transforms.Concatenate("Features", "TaskNameEncoded", nameof(ModelInput.DayOfWeek), nameof(ModelInput.Hour), nameof(ModelInput.Duration)))

                // ▼▼▼ [이 줄을 추가하세요] ▼▼▼
                // 'FocusScore'(정답)를 ML.NET이 인식하는 'Label'로 복사합니다.
                .Append(_mlContext.Transforms.CopyColumns(outputColumnName: "Label", inputColumnName: nameof(ModelInput.FocusScore)));
            // ▲▲▲ [여기까지 추가] ▲▲▲

            // 2. 훈련 알고리즘 선택 (예: LightGbmRegression)
            var trainer = _mlContext.Regression.Trainers.LightGbm(labelColumnName: "Label", featureColumnName: "Features");

            // 3. 전체 파이프라인 결합
            var trainingPipeline = dataProcessPipeline.Append(trainer);

            return trainingPipeline;
        }
        // ▲▲▲ [수정 완료] ▲▲▲

        // (기존 Predict 메서드 - 수정 없음)
        public float Predict(ModelInput input)
        {
            if (_predictionEngine == null)
            {
                Debug.WriteLine("Prediction engine is not initialized.");
                return 0f;
            }
            try
            {
                var prediction = _predictionEngine.Predict(input);
                return prediction.Score;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error during prediction: {ex.Message}");
                return 0f;
            }
        }

        // ▼▼▼ [이 메서드 전체를 새로 추가하세요] ▼▼▼
        /// <summary>
        /// 사용자의 실제 TimeLogEntry 리스트를 기반으로 모델을 훈련하고 .zip 파일로 저장합니다.
        /// </summary>
        public bool TrainModel(List<TimeLogEntry> userLogs)
        {
            try
            {
                // 1. TimeLogEntry -> ModelInput 형태로 변환
                var trainingData = userLogs
                    .Where(log => log.FocusScore > 0) // 점수가 매겨진 로그만 사용
                    .Select(log => new ModelInput
                    {
                        TaskName = log.TaskText,
                        DayOfWeek = (float)log.StartTime.DayOfWeek,
                        Hour = (float)log.StartTime.Hour,
                        Duration = (float)log.Duration.TotalMinutes,
                        FocusScore = (float)log.FocusScore // '정답' 설정
                    }).ToList();

                if (trainingData.Count < 10) // 훈련에 필요한 최소 데이터 수
                {
                    Debug.WriteLine("Not enough data to train model.");
                    return false;
                }

                // 2. ML.NET 데이터 뷰로 변환
                var dataView = _mlContext.Data.LoadFromEnumerable(trainingData);

                // 3. 훈련 파이프라인 가져오기
                var pipeline = BuildTrainingPipeline();

                // 4. 모델 훈련
                Debug.WriteLine("Starting model training...");
                _model = pipeline.Fit(dataView);
                Debug.WriteLine("Model training finished.");

                // 5. 훈련된 모델을 .zip 파일로 저장
                _mlContext.Model.Save(_model, dataView.Schema, DataManager.UserModelFilePath);
                Debug.WriteLine($"Model saved to {DataManager.UserModelFilePath}");

                // 6. 훈련된 모델을 즉시 PredictionEngine에 반영
                _predictionEngine = _mlContext.Model.CreatePredictionEngine<ModelInput, ModelOutput>(_model);
                Debug.WriteLine("Prediction engine updated with new model.");

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error training model: {ex.Message}");
                return false;
            }
        }
        // ▲▲▲ [여기까지 추가] ▲▲▲
    }
}