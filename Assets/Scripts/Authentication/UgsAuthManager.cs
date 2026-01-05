using System;
using System.Threading.Tasks;
using UnityEngine;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Core.Environments;

namespace MazeMates.Authentication
{
    public class UgsAuthManager : MonoBehaviour
    {
        public static UgsAuthManager Instance { get; private set; }

        public event Action SignedIn;
        public event Action SignedOut;
        public event Action<string> AuthError;

        [Header("Testing (Optional)")]
        [SerializeField] private string editorProfile = "default";

        private bool _initializing;
        private bool _eventsHooked;

        public bool IsInitialized => UnityServices.State == ServicesInitializationState.Initialized;
        public bool IsSignedIn => AuthenticationService.Instance != null && AuthenticationService.Instance.IsSignedIn;

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public async Task InitializeAsync(string profileOverride = null, string environment = null)
        {
            if (IsInitialized || _initializing)
            {
                HookAuthEventsIfNeeded();
                return;
            }

            _initializing = true;

            try
            {
                var options = new InitializationOptions();

                var profile = string.IsNullOrWhiteSpace(profileOverride) ? editorProfile : profileOverride;
                if (!string.IsNullOrWhiteSpace(profile))
                    options.SetProfile(profile);

                if (!string.IsNullOrWhiteSpace(environment))
                    options.SetEnvironmentName(environment);

                await UnityServices.InitializeAsync(options);
                HookAuthEventsIfNeeded();
            }
            catch (Exception e)
            {
                AuthError?.Invoke($"UGS init failed: {e.Message}");
            }
            finally
            {
                _initializing = false;
            }
        }

        private void HookAuthEventsIfNeeded()
        {
            if (_eventsHooked) return;
            if (AuthenticationService.Instance == null) return;

            AuthenticationService.Instance.SignedIn += HandleSignedIn;
            AuthenticationService.Instance.SignedOut += HandleSignedOut;
            AuthenticationService.Instance.Expired += HandleExpired;

            _eventsHooked = true;
        }

        private void OnDestroy()
        {
            if (!_eventsHooked) return;
            if (AuthenticationService.Instance == null) return;

            AuthenticationService.Instance.SignedIn -= HandleSignedIn;
            AuthenticationService.Instance.SignedOut -= HandleSignedOut;
            AuthenticationService.Instance.Expired -= HandleExpired;

            _eventsHooked = false;
        }

        public async Task SignInWithUsernamePasswordAsync(string username, string password)
        {
            try
            {
                await EnsureReadyAsync();
                await AuthenticationService.Instance.SignInWithUsernamePasswordAsync(username, password);
            }
            catch (Exception e)
            {
                AuthError?.Invoke($"Login failed: {e.Message}");
            }
        }

        public async Task RegisterWithUsernamePasswordAsync(string username, string password)
        {
            try
            {
                await EnsureReadyAsync();
                await AuthenticationService.Instance.SignUpWithUsernamePasswordAsync(username, password);

                if (!AuthenticationService.Instance.IsSignedIn)
                    await AuthenticationService.Instance.SignInWithUsernamePasswordAsync(username, password);
            }
            catch (Exception e)
            {
                AuthError?.Invoke($"Register failed: {e.Message}");
            }
        }

        public void SignOut(bool clearSession = false)
        {
            try
            {
                if (AuthenticationService.Instance == null) return;
                AuthenticationService.Instance.SignOut(clearSession);
            }
            catch (Exception e)
            {
                AuthError?.Invoke($"SignOut failed: {e.Message}");
            }
        }

        private async Task EnsureReadyAsync()
        {
            if (!IsInitialized)
                await InitializeAsync();

            HookAuthEventsIfNeeded();
        }

        private void HandleSignedIn() => SignedIn?.Invoke();
        private void HandleSignedOut() => SignedOut?.Invoke();

        private void HandleExpired()
        {
            AuthError?.Invoke("Session expired. Please sign in again.");
        }
    }
}
