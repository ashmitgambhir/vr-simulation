using System;
using UnityEngine;

namespace VRSimulation.Core.Data
{
    /// <summary>
    /// Outcome of one knowledge check (backend schema, "Quiz Results").
    /// </summary>
    /// <remarks>
    /// Every attempt is appended rather than overwriting the previous one, because the PRD success
    /// metrics are about whether understanding was reached, and a first-attempt failure followed by
    /// a second-attempt pass is exactly the signal that a module needs rewriting.
    /// </remarks>
    [Serializable]
    public sealed class QuizResultData
    {
        /// <summary>Identifier of the quiz, unique within its module.</summary>
        public int quizId;

        /// <summary>Integer value of the <see cref="ModuleId"/> the quiz belongs to.</summary>
        public int moduleId = (int)ModuleId.None;

        /// <summary>Score as a percentage, 0 to 100. Derived from the counts below.</summary>
        public int score;

        /// <summary>Number of questions presented.</summary>
        public int totalQuestions;

        /// <summary>Number answered correctly.</summary>
        public int correctAnswers;

        /// <summary>ISO-8601 UTC timestamp of submission.</summary>
        public string completedAt = string.Empty;

        /// <summary>Gets or sets <see cref="moduleId"/> as a strongly typed value.</summary>
        public ModuleId Module
        {
            get => ModuleIdExtensions.FromInt(moduleId);
            set => moduleId = (int)value;
        }

        /// <summary>Gets the submission time, or <see cref="DateTime.MinValue"/> if unset.</summary>
        public DateTime CompletedAtUtc => TimestampUtility.Parse(completedAt);

        /// <summary>
        /// Creates a result, computing the percentage score from the answer counts.
        /// </summary>
        /// <param name="quizId">Identifier of the quiz.</param>
        /// <param name="module">Owning module.</param>
        /// <param name="correctAnswers">Number answered correctly.</param>
        /// <param name="totalQuestions">Number presented.</param>
        /// <param name="utcNow">Current UTC time.</param>
        /// <returns>A populated, internally consistent result.</returns>
        public static QuizResultData Create(
            int quizId,
            ModuleId module,
            int correctAnswers,
            int totalQuestions,
            DateTime utcNow)
        {
            int safeTotal = Mathf.Max(0, totalQuestions);
            int safeCorrect = Mathf.Clamp(correctAnswers, 0, safeTotal);

            return new QuizResultData
            {
                quizId = quizId,
                moduleId = (int)module,
                totalQuestions = safeTotal,
                correctAnswers = safeCorrect,
                score = CalculateScore(safeCorrect, safeTotal),
                completedAt = TimestampUtility.Format(utcNow)
            };
        }

        /// <summary>
        /// Computes a percentage score, treating a quiz with no questions as zero rather than
        /// dividing by zero.
        /// </summary>
        /// <param name="correctAnswers">Number answered correctly.</param>
        /// <param name="totalQuestions">Number presented.</param>
        /// <returns>A percentage between 0 and 100.</returns>
        public static int CalculateScore(int correctAnswers, int totalQuestions)
        {
            if (totalQuestions <= 0)
            {
                return 0;
            }

            return Mathf.Clamp(Mathf.RoundToInt(correctAnswers * 100f / totalQuestions), 0, 100);
        }

        /// <summary>
        /// Forces the record into a self-consistent state.
        /// </summary>
        /// <param name="utcNow">Current UTC time, used to backfill an absent timestamp.</param>
        /// <returns><c>true</c> if anything had to be repaired.</returns>
        public bool Sanitize(DateTime utcNow)
        {
            bool repaired = false;

            if (!ModuleIdExtensions.IsDefined(moduleId))
            {
                moduleId = (int)ModuleId.None;
                repaired = true;
            }

            if (totalQuestions < 0)
            {
                totalQuestions = 0;
                repaired = true;
            }

            int clampedCorrect = Mathf.Clamp(correctAnswers, 0, totalQuestions);
            if (clampedCorrect != correctAnswers)
            {
                correctAnswers = clampedCorrect;
                repaired = true;
            }

            // The score is derived, so a stored value that disagrees with the counts is repaired
            // from the counts rather than trusted.
            int expected = CalculateScore(correctAnswers, totalQuestions);
            if (expected != score)
            {
                score = expected;
                repaired = true;
            }

            if (!TimestampUtility.IsValid(completedAt))
            {
                completedAt = TimestampUtility.Format(utcNow);
                repaired = true;
            }

            return repaired;
        }
    }
}
