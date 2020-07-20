declare const Blazor: IBlazor;
declare const DotNet: IDotNet;

interface IDotNet {
    invokeMethodAsync<T>(assemblyName: string, methodIdentifier: string, ...args: any[]): Promise<T>;
}
interface IBlazor {
    platform: IBlazorPlatform;
}
interface MethodHandle { MethodHandle__DO_NOT_IMPLEMENT: any }
interface System_Object { System_Object__DO_NOT_IMPLEMENT: any }
interface System_Array<T> extends System_Object { System_Array__DO_NOT_IMPLEMENT: any }
interface Pointer { Pointer__DO_NOT_IMPLEMENT: any }

interface IBlazorPlatform {
    toJavaScriptString(pointer: any): string;
    toUint8Array(array: System_Array<any>): Uint8Array;
    readInt16Field(baseAddress: Pointer, fieldOffset?: number): number;
    readInt32Field(baseAddress: Pointer, fieldOffset?: number): number;
    readUint64Field(baseAddress: Pointer, fieldOffset?: number): number;
    readFloatField(baseAddress: Pointer, fieldOffset?: number): number;
    readObjectField<T extends System_Object>(baseAddress: Pointer, fieldOffset?: number): T;
    readStringField(baseAddress: Pointer, fieldOffset?: number): string | null;
    readStructField<T extends Pointer>(baseAddress: Pointer, fieldOffset?: number): T;
}

interface IReadFileParams {
    taskId: number;
    buffer: System_Array<any>;
    bufferOffset: number;
    count: number;
    fileRef: number;
    position: number;
};

interface ReadFileSliceResult {
    file: File;
    result: string | ArrayBuffer;
}

interface IFileInfo {
    name: string;
    nonStandardProperties: any;
    size: number;
    type: string;
    lastModified: number;
};

interface IDotNetBuffer {
    toUint8Array(): Uint8Array;
}

class FileReaderComponent {

    private newFileStreamReference = 0;
    private readonly fileStreams: { [reference: number]: File } = {};
    private readonly dragElements: Map<HTMLElement, EventListenerOrEventListenerObject> = new Map();
    private readonly elementDataTransfers: Map<HTMLElement, FileList> = new Map();
    
    private LogIfNull(element: HTMLElement) {
        if (element == null) {
            console.log("BlazorFileReader HTMLElement is null. Can't access IFileReaderRef after HTMLElement was removed from DOM.");
        }
    }
    
    public RegisterDropEvents = (element: HTMLElement, additive: boolean): boolean => {
        this.LogIfNull(element);
        
        const handler = (ev: DragEvent) => {
            this.PreventDefaultHandler(ev);
            if (ev.target instanceof HTMLElement) {
                let list = ev.dataTransfer.files;

                if (additive) {
                    const existing = this.elementDataTransfers.get(element);
                    if (existing !== undefined && existing.length > 0) {
                        list = new FileReaderComponent.ConcatFileList(existing, list);
                    }
                }
                
                this.elementDataTransfers.set(element, list);
            }
        };

        this.dragElements.set(element, handler);
        element.addEventListener("drop", handler);
        element.addEventListener("dragover", this.PreventDefaultHandler);
        return true;
    }

    public UnregisterDropEvents = (element: HTMLElement): boolean => {
        this.LogIfNull(element);
        const handler = this.dragElements.get(element);
        if (handler) {
            element.removeEventListener("drop", handler);
            element.removeEventListener("dragover", this.PreventDefaultHandler);
        }
        this.elementDataTransfers.delete(element);
        this.dragElements.delete(element);
        return true;
    }

    private GetFiles(element: HTMLElement): FileList {
        let files: FileList = null;
        if (element instanceof HTMLInputElement) {
            files = (element as HTMLInputElement).files;
        } else {
            const dataTransfer = this.elementDataTransfers.get(element);
            if (dataTransfer) {
                files = dataTransfer;
            }
        }
        return files;
    }

    public GetFileCount = (element: HTMLElement): number => {
        this.LogIfNull(element);
        const files = this.GetFiles(element);
        if (!files) {
            return -1;
        }
        const result = files.length;
        return result;
    }

    public ClearValue = (element: HTMLInputElement): number => {
        this.LogIfNull(element);
        if (element instanceof HTMLInputElement) {
            element.value = null;
        } else {
            this.elementDataTransfers.delete(element);
        }

        return 0;
    };

    public GetFileInfoFromElement = (element: HTMLElement, index: number): IFileInfo => {
        this.LogIfNull(element);
        const files = this.GetFiles(element);
        if (!files) {
            return null;
        }

        const file = files.item(index);
        if (!file) {
            return null;
        }

        return this.GetFileInfoFromFile(file);
    }

    public Dispose = (fileRef: number): boolean => {
        return delete (this.fileStreams[fileRef]);
    }

    public GetFileInfoFromFile(file: File): IFileInfo {
        const result = {
            lastModified: file.lastModified,
            name: file.name,
            nonStandardProperties: null,
            size: file.size,
            type: file.type
        };
        const properties: { [propertyName: string]: object } = {};
        for (const property in file) {
            if (Object.getPrototypeOf(file).hasOwnProperty(property) && !(property in result)) {
                properties[property] = file[property];
            }
        }
        result.nonStandardProperties = properties;
        return result;
    }

    public OpenRead = (element: HTMLElement, fileIndex: number): number => {
        this.LogIfNull(element);
        
        const files = this.GetFiles(element);
        if (!files) {
            throw 'No FileList available.';
        }
        const file = files.item(fileIndex);
        if (!file) {
            throw `No file with index ${fileIndex} available.`;
        }
            
        const fileRef: number = this.newFileStreamReference++;
        this.fileStreams[fileRef] = file;
        return fileRef;
        
    }
    public ReadFileParamsPointer = (readFileParamsPointer: Pointer): IReadFileParams => {
        return {
            taskId: Blazor.platform.readUint64Field(readFileParamsPointer, 0),
            bufferOffset: Blazor.platform.readUint64Field(readFileParamsPointer, 8),
            count: Blazor.platform.readInt32Field(readFileParamsPointer, 16),
            fileRef: Blazor.platform.readInt32Field(readFileParamsPointer, 20),
            position: Blazor.platform.readUint64Field(readFileParamsPointer, 24),
            buffer: Blazor.platform.readInt32Field(readFileParamsPointer, 32) as unknown as System_Array<any>
        };
    }

    public ReadFileUnmarshalledAsync = (readFileParamsPointer: Pointer) => {
        const readFileParams = this.ReadFileParamsPointer(readFileParamsPointer);

        const asyncCall = new Promise<number>((resolve, reject) => {
            return this.ReadFileSlice(readFileParams, (r,b) => r.readAsArrayBuffer(b))
                .then(r => {
                    try {
                        const dotNetBufferView = Blazor.platform.toUint8Array(readFileParams.buffer);
                        const arrayBuffer = r.result as ArrayBuffer;
                        dotNetBufferView.set(new Uint8Array(arrayBuffer), readFileParams.bufferOffset);

                        const byteCount = Math.min(arrayBuffer.byteLength, readFileParams.count);
                        resolve(byteCount);
                    } catch (e) {
                        reject(e);
                    }
                }, e => reject(e));
        });

        asyncCall.then(
            byteCount => DotNet.invokeMethodAsync("Blazor.FileReader", "EndReadFileUnmarshalledAsyncResult", readFileParams.taskId, byteCount),
            error => {
                console.error("ReadFileUnmarshalledAsync error", error);
                DotNet.invokeMethodAsync("Blazor.FileReader", "EndReadFileUnmarshalledAsyncError", readFileParams.taskId, error.toString());
            });
    }

    public ReadFileMarshalledAsync = (readFileParams: IReadFileParams): Promise<string> => {
        return new Promise<string>((resolve, reject) => {
            return this.ReadFileSlice(readFileParams, (r,b) => r.readAsDataURL(b))
                .then(r => {
                    const contents = r.result as string;
                    const data = contents ? contents.split(";base64,")[1] : null;
                    resolve(data);
                }, e => reject(e));
        });
    }
    

    private ReadFileSlice = (readFileParams: IReadFileParams, method: (reader: FileReader, blob: Blob) => void): Promise<ReadFileSliceResult> => {
        return new Promise<ReadFileSliceResult>((resolve, reject) => {
            const file: File = this.fileStreams[readFileParams.fileRef];
            try {
                const reader = new FileReader();
                reader.onload = ((r) => {
                    return () => {
                        try {
                            resolve({result: r.result, file: file });
                        } catch (e) {
                            reject(e);
                        }
                    }
                })(reader);
                method(reader, file.slice(readFileParams.position, readFileParams.position + readFileParams.count));
            } catch (e) {
                reject(e);
            }
        });
    }

    private PreventDefaultHandler = (ev: DragEvent) => {
        ev.preventDefault();
    }

    static ConcatFileList = class implements FileList {
        [index: number]: File;

        length: number;

        item(index: number): File {
            return this[index];
        }

        constructor(existing: FileList, additions: FileList) {
            for (let i = 0; i < existing.length; i++) {
                this[i] = existing[i];
            }

            const eligebleAdditions = [];

            // Check for doubles
            for (let i = 0; i < additions.length; i++) {
                let exists = false;
                const addition = additions[i];
                for (let j = 0; j < existing.length; j++) {
                    if (existing[j] === addition) {
                        exists = true;
                        break;
                    }
                }

                if (!exists) {
                    eligebleAdditions[eligebleAdditions.length] = addition;
                }
            }

            for (let i = 0; i < eligebleAdditions.length; i++) {
                this[i + existing.length] = eligebleAdditions[i];
            }

            this.length = existing.length + eligebleAdditions.length;
        }
    }
}

(window as any).FileReaderComponent = new FileReaderComponent();
