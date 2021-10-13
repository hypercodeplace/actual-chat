import './virtual-list.css';

export class VirtualList {
    /** ref to div.virtual-list */
    private _elementRef: HTMLElement;
    private _blazorRef: DotNet.DotNetObject;
    private _spacerRef: HTMLElement;
    private _displayedItemsRef: HTMLElement;
    private _abortController: AbortController;

    private _resizeObserver: ResizeObserver;
    private _resizedOnce: Map<Element, boolean>;
    private _updateClientSideStateTask: Promise<unknown> | null;
    private _onScrollStoppedTimeout: any;
    private _onResizeTimeout: any;

    static create(elementRef: HTMLElement, backendRef: DotNet.DotNetObject) {
        return new VirtualList(elementRef, backendRef);
    }

    constructor(elementRef: HTMLElement, backendRef: DotNet.DotNetObject) {
        this._elementRef = elementRef;
        this._blazorRef = backendRef;
        this._abortController = new AbortController();
        this._spacerRef = this._elementRef.querySelector(".spacer")!;
        this._displayedItemsRef = this._elementRef.querySelector(".items-displayed");
        this._updateClientSideStateTask = null!;
        this._onScrollStoppedTimeout = null!;
        this._resizeObserver = new ResizeObserver(entries => this.onResize(entries));
        this._resizedOnce = new Map<Element, boolean>();

        let listenerOptions: AddEventListenerOptions = { signal: this._abortController.signal };
        elementRef.addEventListener("scroll", _ => this.updateClientSideStateAsync(), listenerOptions);
    };

    dispose() {
        this._abortController.abort();
        this._resizeObserver.disconnect();
    }

    afterRender(mustScroll, viewOffset, mustNotifyWhenScrollStops) {
        let spacerSize = this.getSpacerSize();
        // console.log("afterRender: ", { mustScroll, viewOffset, wantResizeSpacer: mustNotifyWhenScrollStops, spacerSize });
        if (mustScroll)
            this._elementRef.scrollTo(0, viewOffset + spacerSize);
        let _ = this.updateClientSideStateAsync();
        this.setupResizeTracking();
        this.setupScrollTracking(mustNotifyWhenScrollStops);
    }

    /** scroll stopped notification */
    setupScrollTracking(mustNotifyWhenScrollStops) {
        if (mustNotifyWhenScrollStops) {
            if (this._onScrollStoppedTimeout == null)
                this.onScroll();
        } else {
            if (this._onScrollStoppedTimeout != null)
                clearTimeout(this._onScrollStoppedTimeout);
            this._onScrollStoppedTimeout = null;
        }
    }

    onScroll() {
        if (this._onScrollStoppedTimeout != null)
            clearTimeout(this._onScrollStoppedTimeout);
        this._onScrollStoppedTimeout = setTimeout(() => this.updateClientSideStateAsync(true), 2000);
    }

    /** setups resize notifications */
    setupResizeTracking() {
        this._resizeObserver.disconnect();
        this._resizedOnce = new Map<Element, boolean>();
        if (this._onResizeTimeout != null)
            clearTimeout(this._onResizeTimeout);
        let items = this._elementRef.querySelectorAll(".items-displayed .item").values();
        this._resizeObserver.observe(this._elementRef);
        for (let item of items)
            this._resizeObserver.observe(item);
    }

    onResize(entries: ResizeObserverEntry[]) {
        let mustIgnore = false;
        for (let entry of entries) {
            if (this._resizedOnce.has(entry.target)) {
                mustIgnore = true;
            }
            this._resizedOnce.set(entry.target, true);
        }
        if (mustIgnore)
            return;

        if (this._onResizeTimeout != null)
            clearTimeout(this._onResizeTimeout);
        this._onResizeTimeout = setTimeout(() => this.updateClientSideStateAsync(), 50);
    }

    /** sends the state to UpdateClientSideState dotnet part */
    async updateClientSideStateAsync(isSafeToScroll = false) {
        if (this._updateClientSideStateTask != null) {
            // this call should run in the same order / non-concurrently
            await this._updateClientSideStateTask.then(v => v, _ => null);
            this._updateClientSideStateTask = null;
        }
        let state: Required<IClientSideState> = {
            RenderIndex: parseInt(this._elementRef.dataset["renderIndex"]!),
            Height: this._elementRef.getBoundingClientRect().height,
            IsSafeToScroll: isSafeToScroll,
            SpacerSize: this.getSpacerSize(),
            ScrollTop: this._elementRef.scrollTop,
            ScrollHeight: this._elementRef.scrollHeight,
            ItemSizes: {},
        };
        if (!isSafeToScroll) {
            let items = this._elementRef.querySelectorAll(".items-unmeasured .item").values() as IterableIterator<HTMLElement>;
            for (let item of items) {
                let key = item.dataset["key"];
                state.ItemSizes[key] = item.getBoundingClientRect().height;
            }
            items = this._elementRef.querySelectorAll(".items-displayed .item").values() as IterableIterator<HTMLElement>;
            for (let item of items) {
                let key = item.dataset["key"];
                let knownSize = parseFloat(item.dataset["size"]!);
                let size = item.getBoundingClientRect().height;
                if (Math.abs(size - knownSize) >= 0.001)
                    state.ItemSizes[key] = size;
            }
        }
        this._updateClientSideStateTask = this._blazorRef.invokeMethodAsync("UpdateClientSideState", state);
    }

    getSpacerSize() {
        let entriesTop = this._displayedItemsRef.getBoundingClientRect().top;
        let spacerTop = this._spacerRef.getBoundingClientRect().top;
        return entriesTop - spacerTop;
    }
}

/** should be in consist with IVirtualListBackend.ClientSideState */
interface IClientSideState {
    RenderIndex: number;

    /** Is Blazor side can call scroll programmly at the moment or not */
    IsSafeToScroll: boolean;

    /** Size of div.spacer */
    SpacerSize: number;

    /** Is used to implement sticky top/bottom */
    ScrollTop: number;

    /** Is used to implement sticky bottom */
    ScrollHeight: number;

    /** Height of div.virtual-list */
    Height: number;

    ItemSizes: Record<string, number>;
}
