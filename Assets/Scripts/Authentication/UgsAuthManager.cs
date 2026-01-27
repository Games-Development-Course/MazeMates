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

        // ✅ נשאר לתאימות עם הקוד שלך
        public event Action SignedIn;
        public event Action SignedOut;
        public event Action<string> AuthError;

        [Header("Testing (Optional)")]
        [SerializeField] private string editorProfile = "default";

        private bool _initializing;
        private bool _eventsHooked;

        // ✅ בשביל ה-UI תרגום אצלך
        public string LastErrorRaw { get; private set; }

        public bool IsInitialized => UnityServices.State == ServicesInitializationState.Initialized;
        public bool IsSignedIn => AuthenticationService.Instance != null && AuthenticationService.Instance.IsSignedIn;

        private void Awake()
        {
            if (Instance != null && Instance != this)
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
                LastErrorRaw = e.ToString();
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

        // -------------------- Auth API (returns bool) --------------------

        public async Task<bool> SignInWithUsernamePasswordAsync(string username, string password)
        {
            LastErrorRaw = null;

            try
            {
                await EnsureReadyAsync();
                await AuthenticationService.Instance.SignInWithUsernamePasswordAsync(username, password);
                return AuthenticationService.Instance.IsSignedIn;
            }
            catch (Exception e)
            {
                LastErrorRaw = e.ToString();
                AuthError?.Invoke($"Login failed: {e.Message}");
                return false;
            }
        }

        public async Task<bool> RegisterWithUsernamePasswordAsync(string username, string password)
        {
            LastErrorRaw = null;

            try
            {
                await EnsureReadyAsync();
                await AuthenticationService.Instance.SignUpWithUsernamePasswordAsync(username, password);

                // לפעמים SignUp לא עושה SignIn, אז נוודא
                if (!AuthenticationService.Instance.IsSignedIn)
                    await AuthenticationService.Instance.SignInWithUsernamePasswordAsync(username, password);

                return AuthenticationService.Instance.IsSignedIn;
            }
            catch (Exception e)
            {
                LastErrorRaw = e.ToString();
                AuthError?.Invoke($"Register failed: {e.Message}");
                return false;
            }
        }

        public async Task<bool> SignInAsGuestAsync()
        {
            LastErrorRaw = null;

            try
            {
                await EnsureReadyAsync();

                if (!AuthenticationService.Instance.IsSignedIn)
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();

                return AuthenticationService.Instance.IsSignedIn;
            }
            catch (Exception e)
            {
                LastErrorRaw = e.ToString();
                AuthError?.Invoke($"Guest sign-in failed: {e.Message}");
                return false;
            }
        }

        // ✅ חזר לתאימות עם RelayUIController וכו'
        public void SignOut(bool clearSession = false)
        {
            try
            {
                if (AuthenticationService.Instance == null) return;
                AuthenticationService.Instance.SignOut(clearSession);
            }
            catch (Exception e)
            {
                LastErrorRaw = e.ToString();
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
