import { Container } from 'pixi.js';
import { getShips, getAsteroids } from '../../state/WorldState';
import { ShipRenderer } from '../entities/ShipRenderer';
import { AsteroidRenderer } from '../entities/AsteroidRenderer';

export class WorldLayer extends Container {
  private shipMap = new Map<number, ShipRenderer>();
  private asteroidMap = new Map<number, AsteroidRenderer>();

  update(): void {
    const ships = getShips();
    const asteroids = getAsteroids();

    // Remove stale ships
    for (const [id, r] of this.shipMap) {
      if (!ships.find(s => s.shipId === id)) {
        this.removeChild(r);
        this.shipMap.delete(id);
      }
    }
    // Add/update ships
    for (const ship of ships) {
      let r = this.shipMap.get(ship.shipId);
      if (!r) {
        r = new ShipRenderer();
        this.addChild(r);
        this.shipMap.set(ship.shipId, r);
      }
      r.update(ship);
    }

    // Remove stale asteroids
    for (const [id, r] of this.asteroidMap) {
      if (!asteroids.find(a => a.asteroidId === id)) {
        this.removeChild(r);
        this.asteroidMap.delete(id);
      }
    }
    // Add/update asteroids
    for (const asteroid of asteroids) {
      let r = this.asteroidMap.get(asteroid.asteroidId);
      if (!r) {
        r = new AsteroidRenderer(asteroid);
        this.addChild(r);
        this.asteroidMap.set(asteroid.asteroidId, r);
      }
      r.update(asteroid);
    }
  }
}
