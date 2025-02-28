import { initializeApp } from 'firebase/app';
import { getMessaging, getToken, GetTokenOptions, onMessage } from 'firebase/messaging';

export async function getDeviceToken(): Promise<string | null> {
    try {
        const response = await fetch('/dist/config/firebase.config.js');
        if (response.ok || response.status === 304) {
            const { config, publicKey } = await response.json();
            const app = initializeApp(config);
            const messaging = getMessaging(app);
            onMessage(messaging, (payload) => {
                console.log('Message received. ', payload);
            });

            const origin = new URL('messaging-init.ts', import.meta.url).origin;
            const swPath = new URL('/dist/messagingServiceWorker.js', origin).toString();
            const configBase64 = btoa(JSON.stringify(config));
            const swUrl = `${swPath}?config=${configBase64}`;
            let swRegistration = await navigator.serviceWorker.getRegistration(swUrl);
            if (!swRegistration) {
                swRegistration = await navigator.serviceWorker.register(swUrl, {
                    scope: '/dist/firebase-cloud-messaging-push-scope'
                });
            }
            const tokenOptions: GetTokenOptions = {
                vapidKey: publicKey,
                serviceWorkerRegistration: swRegistration,
            };
            return await getToken(messaging, tokenOptions);
        } else {
            console.warn(`Unable to initialize messaging subscription. Status: ${response.status}`)
        }
        return null;
    }
    catch(e) {
        console.error(e);
    }
}

export function askNotificationPermission(): boolean {
    // Let's check if the browser supports notifications
    if (!('Notification' in window)) {
        console.log('This browser does not support notifications.');
    } else {
        if (checkNotificationPromise()) {
            Notification.requestPermission()
                .then((permission) => {
                    handlePermission(permission);
                });
        } else {
            // Legacy browsers / safari
            Notification.requestPermission(function(permission) {
                handlePermission(permission);
            });
        }

        return Notification.permission === 'granted';
    }
}

function handlePermission(permission) {
    // Whatever the user answers, we make sure Chrome stores the information
    if (!('permission' in Notification)) {
        // @ts-ignore readonly property
        Notification['permission'] = permission;
    }
}

// Function to check whether browser supports the promise version of requestPermission()
// Safari only supports the old callback-based version
function checkNotificationPromise(): boolean {
    try {
        Notification.requestPermission().then();
    } catch(e) {
        return false;
    }

    return true;
}

// ask for notification permissions on any user interaction
// TODO(AK): move to more natural place, like joining a chat, etc.
// It should be done at JS\TS
function init() {
    self.addEventListener('touchstart', initEventListener);
    self.addEventListener('onkeydown', initEventListener);
    self.addEventListener('mousedown', initEventListener);
    self.addEventListener('pointerdown', initEventListener);
    self.addEventListener('pointerup', initEventListener);
}

function removeInitListeners() {
    self.removeEventListener('touchstart', initEventListener);
    self.removeEventListener('onkeydown', initEventListener);
    self.removeEventListener('mousedown', initEventListener);
    self.removeEventListener('pointerdown', initEventListener);
    self.removeEventListener('pointerup', initEventListener);
}

const initEventListener = () => {
    if (!askNotificationPermission()) {
        console.log('Notifications are disabled.');
    }
};

init();
