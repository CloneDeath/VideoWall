export class FrameApi {
    constructor(frameData) {
        this.frameData = frameData;
    }
    
    get id() { return this.frameData.id; }
    get x() { return this.frameData.location.x; }
    get y() { return this.frameData.location.y; }
    get width() { return this.frameData.location.width; }
    get height() { return this.frameData.location.height; }
    get image() { return this.frameData.image; }
    
    async update() {
        const response = await fetch(`VideoWall/Frames/${this.id}`);
        this.frameData = await response.json()
    }
    
    async updatePosition(x, y) {
        this.frameData.location.x = x;
        this.frameData.location.y = y;
        const response = await fetch(`VideoWall/Frames/${this.id}`, {
            method: 'PUT',
            headers: {'Content-Type': 'application/json'},
            body: JSON.stringify(this.frameData.location)
        });
    }
}