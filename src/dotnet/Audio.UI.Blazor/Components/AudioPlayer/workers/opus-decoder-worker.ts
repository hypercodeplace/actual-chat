import { CreateDecoderMessage, DataDecoderMessage, DecoderMessage, EndDecoderMessage, InitDecoderMessage, OperationCompletedDecoderWorkerMessage, StopDecoderMessage } from './opus-decoder-worker-message';
import { OpusDecoder } from './opus-decoder';

const LogScope: string = 'OpusDecoderWorker'
const worker = self as unknown as Worker;
const decoders = new Map<number, OpusDecoder>();
const debug = false;
const debugPushes: boolean = debug && false;

worker.onmessage = async (ev: MessageEvent<DecoderMessage>): Promise<void> => {
    try {
        const msg = ev.data;
        switch (msg.type) {
        case 'create':
            await onCreate(msg as CreateDecoderMessage);
            break;
        case 'init':
            onInit(msg as InitDecoderMessage);
            break;
        case 'data':
            onData(msg as DataDecoderMessage);
            break;
        case 'end':
            onEnd(msg as EndDecoderMessage);
            break;
        case 'stop':
            onStop(msg as StopDecoderMessage);
            break;
        default:
            throw new Error(`Unsupported message type: ${msg.type}`);
        }
    }
    catch (error) {
        console.error(`${LogScope}.worker.onmessage error:`, error);
    }
};

function getDecoder(controllerId: number): OpusDecoder {
    const decoder = decoders.get(controllerId);
    if (decoder === undefined) {
        throw new Error(`Can't find decoder object for controller #${controllerId}`);
    }
    return decoder;
}

async function onCreate(message: CreateDecoderMessage) {
    const { callbackId, workletPort, controllerId } = message;
    // decoders are pooled with the parent object, so we don't need an object pool here
    if (debug)
        console.debug(`${LogScope}.onCreate(#${controllerId}): onCreate`);
    const decoder = await OpusDecoder.create(workletPort);
    decoders.set(controllerId, decoder);
    const msg: OperationCompletedDecoderWorkerMessage = {
        type: 'operationCompleted',
        callbackId: callbackId,
    };
    worker.postMessage(msg);
    if (debug)
        console.debug(`${LogScope}.onCreate(#${controllerId}): onCreate completed`);
}

function onInit(message: InitDecoderMessage): void {
    const { callbackId, controllerId } = message;
    const decoder = getDecoder(controllerId);
    if (debug)
        console.debug(`${LogScope}.onCreate(#${controllerId}): onInit`);
    decoder.init();

    const msg: OperationCompletedDecoderWorkerMessage = {
        type: 'operationCompleted',
        callbackId: callbackId,
    };
    worker.postMessage(msg);
    if (debug)
        console.debug(`${LogScope}.onCreate(#${controllerId}): onInit completed`);
}

function onData(message: DataDecoderMessage): void {
    const { controllerId, buffer, offset, length } = message;
    const decoder = getDecoder(controllerId);
    const data = buffer.slice(offset, offset + length);
    if (debugPushes)
        console.debug(`${LogScope}.onData(#${controllerId}): pushing ${data.byteLength} byte(s)`);
    decoder.pushData(data);
}

function onEnd(message: EndDecoderMessage): void {
    const { controllerId } = message;
    const decoder = getDecoder(controllerId);
    if (debug)
        console.debug(`${LogScope}.onCreate(#${controllerId}): onEnd`);
    decoder.pushEnd();
}

function onStop(message: StopDecoderMessage): void {
    const { controllerId } = message;
    const decoder = getDecoder(controllerId);
    if (debug)
        console.debug(`${LogScope}.onCreate(#${controllerId}): onStop`);
    decoder.stop();
    if (debug)
        console.debug(`${LogScope}.onCreate(#${controllerId}): onStop completed`);
}
/// #if DEBUG
self['getDecoder'] = getDecoder;
/// #endif
