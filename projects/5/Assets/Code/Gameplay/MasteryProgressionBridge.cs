using System;
using DunGen.Events;
using DunGen.Networking;
using UnityEngine;

namespace DunGen.Gameplay
{
    /// <summary>
    /// Bridges gameplay progression events to backend mastery APIs.
    /// On level-up, requests a 3-option mastery offer for the configured item type.
    /// UI can call SelectCurrentOfferByIndex when the user picks an option.
    /// </summary>
    public sealed class MasteryProgressionBridge : MonoBehaviour
    {
        [SerializeField] private AuthoritativeSessionClient sessionClient;
        [SerializeField] private string userId = "player-001";
        [SerializeField] private string primaryItemType = "sword";

        public event Action<UnityMasteryOfferDto> OfferReady;
        public event Action<UnityMasterySelectionResultDto> SelectionApplied;

        private UnityMasteryOfferDto _currentOffer;

        private void OnEnable()
        {
            if (sessionClient != null)
            {
                sessionClient.MasteryOfferUpdated += HandleMasteryOffer;
                sessionClient.MasterySelectionCompleted += HandleMasterySelection;
            }

            EventBus.Instance.Subscribe<LevelUpEventData>(OnLevelUp);
        }

        private void OnDisable()
        {
            if (sessionClient != null)
            {
                sessionClient.MasteryOfferUpdated -= HandleMasteryOffer;
                sessionClient.MasterySelectionCompleted -= HandleMasterySelection;
            }

            EventBus.Instance.Unsubscribe<LevelUpEventData>(OnLevelUp);
        }

        public UnityMasteryOfferDto GetCurrentOffer() => _currentOffer;

        public bool SelectCurrentOfferByIndex(int index)
        {
            if (sessionClient == null || _currentOffer == null || _currentOffer.options == null)
                return false;

            if (index < 0 || index >= _currentOffer.options.Length)
                return false;

            var option = _currentOffer.options[index];
            if (option == null || string.IsNullOrWhiteSpace(option.skillId))
                return false;

            sessionClient.SelectMasteryOption(userId, _currentOffer.offerId, option.skillId);
            return true;
        }

        public void RequestOfferNow(string itemType, int playerLevel)
        {
            if (sessionClient == null)
                return;

            var tier = TierFromLevel(playerLevel);
            var normalizedItemType = string.IsNullOrWhiteSpace(itemType) ? primaryItemType : itemType;
            sessionClient.RequestMasteryOffer(userId, normalizedItemType, tier);
        }

        private void OnLevelUp(LevelUpEventData evt)
        {
            if (sessionClient == null)
                return;

            var tier = TierFromLevel(evt.NewLevel);
            sessionClient.RequestMasteryOffer(userId, primaryItemType, tier);
        }

        private void HandleMasteryOffer(UnityMasteryOfferDto offer)
        {
            _currentOffer = offer;
            OfferReady?.Invoke(offer);
        }

        private void HandleMasterySelection(UnityMasterySelectionResultDto result)
        {
            if (_currentOffer != null && string.Equals(_currentOffer.offerId, result.offerId, StringComparison.Ordinal))
                _currentOffer = null;

            SelectionApplied?.Invoke(result);
        }

        private static string TierFromLevel(int level)
        {
            if (level <= 5) return "apprentice";
            if (level <= 10) return "journeyman";
            if (level <= 15) return "craftsman";
            if (level <= 20) return "master";
            if (level <= 30) return "grandmaster";
            if (level <= 45) return "legendary";
            return "god";
        }
    }
}
