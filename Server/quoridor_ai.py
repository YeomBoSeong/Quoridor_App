"""
Quoridor AI - Complete Implementation
모든 AI 컴포넌트를 하나의 파일로 통합

Author: Claude
Date: 2025
License: Commercial Use Allowed
"""

from typing import Tuple, Optional, List, Set, Dict
from collections import deque
from copy import deepcopy
import time
from functools import lru_cache


# ============================================================================
# SECTION 1: Game State Management
# ============================================================================

class QuoridorGameState:
    """
    Quoridor 게임 상태를 관리하는 클래스
    - 17x17 보드 (Unity 좌표계와 동일)
    - 짝수 좌표(0,2,4,...,16): 플레이어 위치
    - 홀수 좌표(1,3,5,...,15): 벽 위치
    - Red는 아래(y=16)에서 시작, 위(y=0)로 이동하면 승리
    - Blue는 위(y=0)에서 시작, 아래(y=16)로 이동하면 승리
    """

    BOARD_SIZE = 17
    INITIAL_WALLS = 10

    def __init__(self):
        """게임 상태 초기화"""
        # 플레이어 위치 (y, x) - 짝수 좌표만 사용
        self.red_pos = (16, 8)   # Red는 아래 중앙에서 시작
        self.blue_pos = (0, 8)   # Blue는 위 중앙에서 시작

        # 남은 벽 개수
        self.red_walls = self.INITIAL_WALLS
        self.blue_walls = self.INITIAL_WALLS

        # 벽 배치 (가로 벽과 세로 벽을 별도로 관리)
        self.horizontal_walls: Set[Tuple[int, int]] = set()
        self.vertical_walls: Set[Tuple[int, int]] = set()

        # 현재 턴 ('red' 또는 'blue')
        self.current_player = 'red'

    def copy(self) -> 'QuoridorGameState':
        """게임 상태의 깊은 복사본 반환"""
        new_state = QuoridorGameState()
        new_state.red_pos = self.red_pos
        new_state.blue_pos = self.blue_pos
        new_state.red_walls = self.red_walls
        new_state.blue_walls = self.blue_walls
        new_state.horizontal_walls = self.horizontal_walls.copy()
        new_state.vertical_walls = self.vertical_walls.copy()
        new_state.current_player = self.current_player
        return new_state

    def get_player_position(self, player: str) -> Tuple[int, int]:
        """플레이어 위치 반환"""
        return self.red_pos if player == 'red' else self.blue_pos

    def get_opponent(self, player: str) -> str:
        """상대 플레이어 반환"""
        return 'blue' if player == 'red' else 'red'

    def get_player_walls(self, player: str) -> int:
        """플레이어의 남은 벽 개수 반환"""
        return self.red_walls if player == 'red' else self.blue_walls

    def get_goal_line(self, player: str) -> int:
        """플레이어의 목표 y 좌표 반환"""
        return 0 if player == 'red' else 16

    def is_goal(self, player: str) -> bool:
        """플레이어가 목표에 도달했는지 확인"""
        pos = self.get_player_position(player)
        goal = self.get_goal_line(player)
        return pos[0] == goal

    def is_valid_position(self, y: int, x: int) -> bool:
        """좌표가 보드 내에 있는지 확인"""
        return 0 <= y < self.BOARD_SIZE and 0 <= x < self.BOARD_SIZE

    def is_wall_between(self, y1: int, x1: int, y2: int, x2: int) -> bool:
        """
        두 칸 사이에 벽이 있는지 확인 (17x17 좌표계)
        플레이어는 짝수 좌표에만 있고, 2칸씩 이동함
        """
        # 위로 이동 (y1 - 2)
        if y2 == y1 - 2 and x2 == x1:
            wall_y = y1 - 1  # 사이의 홀수 좌표
            # 가로 벽이 (wall_y, x1-1), (wall_y, x1), (wall_y, x1+1) 중 하나에 있으면 막힘
            return ((wall_y, x1) in self.horizontal_walls or
                    (wall_y, x1 - 1) in self.horizontal_walls or
                    (wall_y, x1 + 1) in self.horizontal_walls)

        # 아래로 이동 (y1 + 2)
        if y2 == y1 + 2 and x2 == x1:
            wall_y = y1 + 1  # 사이의 홀수 좌표
            return ((wall_y, x1) in self.horizontal_walls or
                    (wall_y, x1 - 1) in self.horizontal_walls or
                    (wall_y, x1 + 1) in self.horizontal_walls)

        # 왼쪽으로 이동 (x1 - 2)
        if y2 == y1 and x2 == x1 - 2:
            wall_x = x1 - 1  # 사이의 홀수 좌표
            # 세로 벽이 (y1-1, wall_x), (y1, wall_x), (y1+1, wall_x) 중 하나에 있으면 막힘
            return ((y1, wall_x) in self.vertical_walls or
                    (y1 - 1, wall_x) in self.vertical_walls or
                    (y1 + 1, wall_x) in self.vertical_walls)

        # 오른쪽으로 이동 (x1 + 2)
        if y2 == y1 and x2 == x1 + 2:
            wall_x = x1 + 1  # 사이의 홀수 좌표
            return ((y1, wall_x) in self.vertical_walls or
                    (y1 - 1, wall_x) in self.vertical_walls or
                    (y1 + 1, wall_x) in self.vertical_walls)

        return False

    def can_move_to(self, from_y: int, from_x: int, to_y: int, to_x: int) -> bool:
        """이동 가능한지 확인 (17x17 좌표계: 2칸씩 이동)"""
        if not self.is_valid_position(to_y, to_x):
            return False
        dy = abs(to_y - from_y)
        dx = abs(to_x - from_x)
        # 17x17 좌표계에서는 2칸씩 이동
        if not ((dy == 2 and dx == 0) or (dy == 0 and dx == 2)):
            return False
        if self.is_wall_between(from_y, from_x, to_y, to_x):
            return False
        return True

    def get_valid_moves(self, player: str) -> List[Tuple[int, int]]:
        """플레이어가 이동할 수 있는 모든 유효한 위치 반환 (점프 포함, 17x17 좌표계)"""
        y, x = self.get_player_position(player)
        opponent_pos = self.get_player_position(self.get_opponent(player))
        valid_moves = []

        # 17x17 좌표계에서는 2칸씩 이동
        directions = [(0, 2), (0, -2), (2, 0), (-2, 0)]

        for dy, dx in directions:
            ny, nx = y + dy, x + dx

            if not self.can_move_to(y, x, ny, nx):
                continue

            if (ny, nx) == opponent_pos:
                # 점프 시도 (상대방을 넘어서 2칸 더 이동)
                jump_y, jump_x = ny + dy, nx + dx
                if (self.is_valid_position(jump_y, jump_x) and
                    not self.is_wall_between(ny, nx, jump_y, jump_x)):
                    valid_moves.append((jump_y, jump_x))
                else:
                    # 대각선 점프 (2칸씩)
                    if dy != 0:
                        for side_dx in [-2, 2]:
                            side_x = nx + side_dx
                            if (self.is_valid_position(ny, side_x) and
                                not self.is_wall_between(ny, nx, ny, side_x)):
                                valid_moves.append((ny, side_x))
                    else:
                        for side_dy in [-2, 2]:
                            side_y = ny + side_dy
                            if (self.is_valid_position(side_y, nx) and
                                not self.is_wall_between(ny, nx, side_y, nx)):
                                valid_moves.append((side_y, nx))
            else:
                valid_moves.append((ny, nx))

        return valid_moves

    def can_place_wall(self, wall_type: str, y: int, x: int) -> bool:
        """
        벽을 배치할 수 있는지 확인 (17x17 좌표계)
        벽은 홀수 좌표(1,3,5,...,15)에만 배치 가능
        """
        # 홀수 좌표 범위 확인 (1~15)
        if not (1 <= y <= 15 and 1 <= x <= 15):
            return False

        # 홀수 좌표인지 확인
        if y % 2 == 0 or x % 2 == 0:
            return False

        if wall_type == 'horizontal':
            # 이미 같은 위치에 벽이 있는지 확인
            if (y, x) in self.horizontal_walls:
                return False
            if (y, x) in self.vertical_walls:
                return False
            # 가로 벽은 좌우로 2칸씩 겹치면 안됨
            if (y, x - 2) in self.horizontal_walls:
                return False
            if (y, x + 2) in self.horizontal_walls:
                return False
        else:  # vertical
            # 이미 같은 위치에 벽이 있는지 확인
            if (y, x) in self.vertical_walls:
                return False
            if (y, x) in self.horizontal_walls:
                return False
            # 세로 벽은 위아래로 2칸씩 겹치면 안됨
            if (y - 2, x) in self.vertical_walls:
                return False
            if (y + 2, x) in self.vertical_walls:
                return False

        # 경로 검증: 벽을 놓아도 양쪽 플레이어 모두 목표에 도달할 수 있어야 함
        # 임시로 벽 배치
        if wall_type == 'horizontal':
            self.horizontal_walls.add((y, x))
        else:
            self.vertical_walls.add((y, x))

        # 양쪽 플레이어가 목표에 도달할 수 있는지 확인
        red_can_reach = shortest_distance_to_goal(self, 'red') < 999
        blue_can_reach = shortest_distance_to_goal(self, 'blue') < 999

        # 임시 벽 제거
        if wall_type == 'horizontal':
            self.horizontal_walls.discard((y, x))
        else:
            self.vertical_walls.discard((y, x))

        if not (red_can_reach and blue_can_reach):
            return False

        return True

    def place_wall(self, player: str, wall_type: str, y: int, x: int) -> bool:
        """벽 배치"""
        walls_left = self.get_player_walls(player)
        if walls_left <= 0:
            return False

        if not self.can_place_wall(wall_type, y, x):
            return False

        if wall_type == 'horizontal':
            self.horizontal_walls.add((y, x))
        else:
            self.vertical_walls.add((y, x))

        if player == 'red':
            self.red_walls -= 1
        else:
            self.blue_walls -= 1

        return True

    def remove_wall(self, wall_type: str, y: int, x: int):
        """벽 제거 (되돌리기용)"""
        if wall_type == 'horizontal':
            self.horizontal_walls.discard((y, x))
        else:
            self.vertical_walls.discard((y, x))

    def make_move(self, player: str, y: int, x: int) -> bool:
        """플레이어 이동"""
        valid_moves = self.get_valid_moves(player)
        if (y, x) not in valid_moves:
            return False

        if player == 'red':
            self.red_pos = (y, x)
        else:
            self.blue_pos = (y, x)

        self.current_player = self.get_opponent(player)
        return True

    def make_wall_move(self, player: str, wall_type: str, y: int, x: int) -> bool:
        """벽 배치 후 턴 변경"""
        if self.place_wall(player, wall_type, y, x):
            self.current_player = self.get_opponent(player)
            return True
        return False

    def parse_position(self, pos_str: str) -> Tuple[int, int]:
        """문자열을 (y, x) 튜플로 변환"""
        parts = pos_str.split(',')
        y = int(parts[0])
        x = int(parts[1])
        return (y, x)

    def position_to_string(self, y: int, x: int) -> str:
        """(y, x) 튜플을 문자열로 변환"""
        return f"{y},{x}"

    def get_hash(self) -> int:
        """게임 상태의 해시값 반환 (캐싱용)"""
        return hash((
            self.red_pos,
            self.blue_pos,
            self.red_walls,
            self.blue_walls,
            frozenset(self.horizontal_walls),
            frozenset(self.vertical_walls)
        ))

    def __repr__(self) -> str:
        """게임 상태를 문자열로 표현"""
        board = [['.' for _ in range(self.BOARD_SIZE)] for _ in range(self.BOARD_SIZE)]
        ry, rx = self.red_pos
        by, bx = self.blue_pos
        board[ry][rx] = 'R'
        board[by][bx] = 'B'

        result = []
        result.append(f"Current Player: {self.current_player}")
        result.append(f"Red Walls: {self.red_walls}, Blue Walls: {self.blue_walls}")
        result.append("  " + " ".join(str(i) for i in range(self.BOARD_SIZE)))

        for y in range(self.BOARD_SIZE):
            row = f"{y} " + " ".join(board[y][x] for x in range(self.BOARD_SIZE))
            result.append(row)

            if y < self.BOARD_SIZE - 1:
                wall_row = "  "
                for x in range(self.BOARD_SIZE):
                    if (y, x) in self.horizontal_walls:
                        wall_row += "─ "
                    else:
                        wall_row += "  "
                result.append(wall_row)

        return "\n".join(result)


# ============================================================================
# SECTION 2: Pathfinding Algorithms
# ============================================================================

class PathfindingCache:
    """BFS 결과를 캐싱하여 성능 향상"""

    def __init__(self, max_size: int = 10000):
        self.distance_cache: Dict[Tuple[int, str], int] = {}
        self.path_cache: Dict[Tuple[int, str], List[Tuple[int, int]]] = {}
        self.max_size = max_size

    def clear(self):
        """캐시 초기화"""
        self.distance_cache.clear()
        self.path_cache.clear()

    def get_distance(self, state_hash: int, player: str) -> Optional[int]:
        """캐시에서 거리 가져오기"""
        return self.distance_cache.get((state_hash, player))

    def set_distance(self, state_hash: int, player: str, distance: int):
        """캐시에 거리 저장"""
        if len(self.distance_cache) >= self.max_size:
            # 캐시가 가득 차면 오래된 항목 일부 제거
            keys_to_remove = list(self.distance_cache.keys())[:self.max_size // 4]
            for key in keys_to_remove:
                self.distance_cache.pop(key, None)

        self.distance_cache[(state_hash, player)] = distance

    def get_path(self, state_hash: int, player: str) -> Optional[List[Tuple[int, int]]]:
        """캐시에서 경로 가져오기"""
        return self.path_cache.get((state_hash, player))

    def set_path(self, state_hash: int, player: str, path: List[Tuple[int, int]]):
        """캐시에 경로 저장"""
        if len(self.path_cache) >= self.max_size:
            # 캐시가 가득 차면 오래된 항목 일부 제거
            keys_to_remove = list(self.path_cache.keys())[:self.max_size // 4]
            for key in keys_to_remove:
                self.path_cache.pop(key, None)

        self.path_cache[(state_hash, player)] = path


# 전역 캐시 인스턴스
_pathfinding_cache = PathfindingCache()


def shortest_distance_to_goal(state: QuoridorGameState, player: str, use_cache: bool = True) -> int:
    """BFS를 사용하여 플레이어가 목표까지 도달하는 최단 거리 계산 (17x17 좌표계)"""
    # 캐시 확인
    if use_cache:
        state_hash = state.get_hash()
        cached_distance = _pathfinding_cache.get_distance(state_hash, player)
        if cached_distance is not None:
            return cached_distance

    start_pos = state.get_player_position(player)
    goal_line = state.get_goal_line(player)

    queue = deque([(start_pos, 0)])
    visited: Set[Tuple[int, int]] = {start_pos}

    while queue:
        (y, x), distance = queue.popleft()

        if y == goal_line:
            # 캐시에 저장
            if use_cache:
                _pathfinding_cache.set_distance(state_hash, player, distance)
            return distance

        # 17x17 좌표계에서는 2칸씩 이동
        for dy, dx in [(0, 2), (0, -2), (2, 0), (-2, 0)]:
            ny, nx = y + dy, x + dx

            if (state.is_valid_position(ny, nx) and
                (ny, nx) not in visited and
                state.can_move_to(y, x, ny, nx)):

                visited.add((ny, nx))
                queue.append(((ny, nx), distance + 1))

    # 캐시에 저장
    if use_cache:
        _pathfinding_cache.set_distance(state_hash, player, 999)
    return 999


def get_shortest_path(state: QuoridorGameState, player: str, use_cache: bool = True) -> List[Tuple[int, int]]:
    """BFS를 사용하여 플레이어가 목표까지 가는 최단 경로 반환 (17x17 좌표계)"""
    # 캐시 확인
    if use_cache:
        state_hash = state.get_hash()
        cached_path = _pathfinding_cache.get_path(state_hash, player)
        if cached_path is not None:
            return cached_path

    start_pos = state.get_player_position(player)
    goal_line = state.get_goal_line(player)

    queue = deque([(start_pos, [start_pos])])
    visited: Set[Tuple[int, int]] = {start_pos}

    while queue:
        (y, x), path = queue.popleft()

        if y == goal_line:
            # 캐시에 저장
            if use_cache:
                _pathfinding_cache.set_path(state_hash, player, path)
            return path

        # 17x17 좌표계에서는 2칸씩 이동
        for dy, dx in [(0, 2), (0, -2), (2, 0), (-2, 0)]:
            ny, nx = y + dy, x + dx

            if (state.is_valid_position(ny, nx) and
                (ny, nx) not in visited and
                state.can_move_to(y, x, ny, nx)):

                visited.add((ny, nx))
                new_path = path + [(ny, nx)]
                queue.append(((ny, nx), new_path))

    # 캐시에 저장
    if use_cache:
        _pathfinding_cache.set_path(state_hash, player, [])
    return []


def has_valid_path_to_goal(state: QuoridorGameState, player: str) -> bool:
    """플레이어가 목표에 도달할 수 있는 경로가 존재하는지 확인"""
    return shortest_distance_to_goal(state, player) < 999


def count_paths_to_goal(state: QuoridorGameState, player: str, max_paths: int = 10) -> int:
    """플레이어가 목표까지 가는 경로의 개수 카운트 (17x17 좌표계)"""
    start_pos = state.get_player_position(player)
    goal_line = state.get_goal_line(player)

    path_count = [0]
    visited_in_current_path: Set[Tuple[int, int]] = set()

    def dfs(y: int, x: int) -> None:
        if y == goal_line:
            path_count[0] += 1
            return

        if path_count[0] >= max_paths:
            return

        visited_in_current_path.add((y, x))

        # 17x17 좌표계에서는 2칸씩 이동
        for dy, dx in [(0, 2), (0, -2), (2, 0), (-2, 0)]:
            ny, nx = y + dy, x + dx

            if (state.is_valid_position(ny, nx) and
                (ny, nx) not in visited_in_current_path and
                state.can_move_to(y, x, ny, nx)):

                dfs(ny, nx)

        visited_in_current_path.remove((y, x))

    dfs(start_pos[0], start_pos[1])
    return min(path_count[0], max_paths)


# ============================================================================
# SECTION 3: Move Generation
# ============================================================================

class Move:
    """Quoridor에서의 수(move)를 나타내는 클래스"""

    def __init__(self, move_type: str, **kwargs):
        self.move_type = move_type

        if move_type == 'move':
            self.y = kwargs['y']
            self.x = kwargs['x']
            self.wall_type = None
        elif move_type == 'wall':
            self.wall_type = kwargs['wall_type']
            self.y = kwargs['y']
            self.x = kwargs['x']
        else:
            raise ValueError(f"Invalid move_type: {move_type}")

    def __repr__(self) -> str:
        if self.move_type == 'move':
            return f"Move({self.y},{self.x})"
        else:
            return f"Wall({self.wall_type},{self.y},{self.x})"

    def __eq__(self, other) -> bool:
        if not isinstance(other, Move):
            return False
        if self.move_type != other.move_type:
            return False
        if self.move_type == 'move':
            return self.y == other.y and self.x == other.x
        else:
            return (self.wall_type == other.wall_type and
                    self.y == other.y and self.x == other.x)

    def __hash__(self) -> int:
        if self.move_type == 'move':
            return hash(('move', self.y, self.x))
        else:
            return hash(('wall', self.wall_type, self.y, self.x))


def generate_smart_moves(state: QuoridorGameState, player: str, max_wall_moves: int = 20) -> List[Move]:
    """좋은 수만 선택적으로 생성 (성능 최적화)"""
    moves = []
    opponent = state.get_opponent(player)

    # 1. 모든 이동 수는 포함
    valid_positions = state.get_valid_moves(player)
    for y, x in valid_positions:
        moves.append(Move('move', y=y, x=x))

    # 2. 전략적 벽만 선택
    if state.get_player_walls(player) > 0:
        wall_candidates = []
        opponent_path = get_shortest_path(state, opponent)

        for i in range(min(5, len(opponent_path) - 1)):
            py, px = opponent_path[i]

            for dy in range(-1, 2):
                for dx in range(-1, 2):
                    wy, wx = py + dy, px + dx

                    if not (0 <= wy < state.BOARD_SIZE - 1 and 0 <= wx < state.BOARD_SIZE - 1):
                        continue

                    # 가로 벽
                    if state.can_place_wall('horizontal', wy, wx):
                        state.horizontal_walls.add((wy, wx))
                        if has_valid_path_to_goal(state, 'red') and has_valid_path_to_goal(state, 'blue'):
                            new_dist = shortest_distance_to_goal(state, opponent)
                            old_dist = len(opponent_path) - 1
                            score = new_dist - old_dist
                            wall_candidates.append((score, Move('wall', wall_type='horizontal', y=wy, x=wx)))
                        state.horizontal_walls.remove((wy, wx))

                    # 세로 벽
                    if state.can_place_wall('vertical', wy, wx):
                        state.vertical_walls.add((wy, wx))
                        if has_valid_path_to_goal(state, 'red') and has_valid_path_to_goal(state, 'blue'):
                            new_dist = shortest_distance_to_goal(state, opponent)
                            old_dist = len(opponent_path) - 1
                            score = new_dist - old_dist
                            wall_candidates.append((score, Move('wall', wall_type='vertical', y=wy, x=wx)))
                        state.vertical_walls.remove((wy, wx))

        wall_candidates.sort(key=lambda x: x[0], reverse=True)
        for score, wall_move in wall_candidates[:max_wall_moves]:
            moves.append(wall_move)

    return moves


def apply_move(state: QuoridorGameState, player: str, move: Move) -> QuoridorGameState:
    """수를 적용한 새로운 게임 상태 반환"""
    new_state = state.copy()

    if move.move_type == 'move':
        new_state.make_move(player, move.y, move.x)
    elif move.move_type == 'wall':
        new_state.make_wall_move(player, move.wall_type, move.y, move.x)

    return new_state


def order_moves(state: QuoridorGameState, player: str, moves: List[Move]) -> List[Move]:
    """수들을 좋을 것 같은 순서로 정렬"""
    opponent = state.get_opponent(player)
    scored_moves = []

    my_current_dist = shortest_distance_to_goal(state, player)
    opponent_current_dist = shortest_distance_to_goal(state, opponent)

    for move in moves:
        score = 0

        if move.move_type == 'move':
            if player == 'red':
                old_pos = state.red_pos
                state.red_pos = (move.y, move.x)
            else:
                old_pos = state.blue_pos
                state.blue_pos = (move.y, move.x)

            new_dist = shortest_distance_to_goal(state, player)
            score = (my_current_dist - new_dist) * 10

            if player == 'red':
                state.red_pos = old_pos
            else:
                state.blue_pos = old_pos

        elif move.move_type == 'wall':
            if move.wall_type == 'horizontal':
                state.horizontal_walls.add((move.y, move.x))
            else:
                state.vertical_walls.add((move.y, move.x))

            new_opponent_dist = shortest_distance_to_goal(state, opponent)
            score = (new_opponent_dist - opponent_current_dist) * 5

            state.remove_wall(move.wall_type, move.y, move.x)

        scored_moves.append((score, move))

    scored_moves.sort(key=lambda x: x[0], reverse=True)
    return [move for score, move in scored_moves]


# ============================================================================
# SECTION 4: Evaluation Functions
# ============================================================================

DISTANCE_WEIGHT = 100
WALL_COUNT_WEIGHT = 10
PATH_COUNT_WEIGHT = 5
CENTER_WEIGHT = 2
MOBILITY_WEIGHT = 3


def evaluate_position(state: QuoridorGameState, player: str, difficulty: str = 'medium') -> float:
    """현재 포지션을 평가하는 함수"""
    if state.is_goal(player):
        return 99999.0

    opponent = state.get_opponent(player)
    if state.is_goal(opponent):
        return -99999.0

    if difficulty == 'easy':
        return evaluate_basic(state, player)
    elif difficulty == 'medium':
        return evaluate_intermediate(state, player)
    else:  # hard - Medium과 동일한 평가 함수 사용 (성능 최적화)
        return evaluate_intermediate(state, player)


def evaluate_basic(state: QuoridorGameState, player: str) -> float:
    """기본 평가 함수 (Easy 난이도)"""
    opponent = state.get_opponent(player)

    my_distance = shortest_distance_to_goal(state, player)
    opponent_distance = shortest_distance_to_goal(state, opponent)

    score = (opponent_distance - my_distance) * DISTANCE_WEIGHT

    return score


def evaluate_intermediate(state: QuoridorGameState, player: str) -> float:
    """중급 평가 함수 (Medium 난이도)"""
    opponent = state.get_opponent(player)
    score = 0.0

    my_distance = shortest_distance_to_goal(state, player)
    opponent_distance = shortest_distance_to_goal(state, opponent)
    score += (opponent_distance - my_distance) * DISTANCE_WEIGHT

    my_walls = state.get_player_walls(player)
    opponent_walls = state.get_player_walls(opponent)
    score += (my_walls - opponent_walls) * WALL_COUNT_WEIGHT

    my_moves = len(state.get_valid_moves(player))
    opponent_moves = len(state.get_valid_moves(opponent))
    score += (my_moves - opponent_moves) * MOBILITY_WEIGHT

    return score


def evaluate_advanced(state: QuoridorGameState, player: str) -> float:
    """고급 평가 함수 (Hard 난이도)"""
    opponent = state.get_opponent(player)
    score = 0.0

    my_distance = shortest_distance_to_goal(state, player)
    opponent_distance = shortest_distance_to_goal(state, opponent)
    distance_diff = opponent_distance - my_distance
    score += distance_diff * DISTANCE_WEIGHT

    my_walls = state.get_player_walls(player)
    opponent_walls = state.get_player_walls(opponent)
    wall_diff = my_walls - opponent_walls

    game_phase = get_game_phase(state)
    if game_phase == 'early':
        score += wall_diff * WALL_COUNT_WEIGHT * 1.5
    elif game_phase == 'mid':
        score += wall_diff * WALL_COUNT_WEIGHT
    else:
        score += wall_diff * WALL_COUNT_WEIGHT * 0.5

    my_path_count = count_paths_to_goal(state, player, max_paths=5)
    opponent_path_count = count_paths_to_goal(state, opponent, max_paths=5)
    score += (my_path_count - opponent_path_count) * PATH_COUNT_WEIGHT

    my_moves = len(state.get_valid_moves(player))
    opponent_moves = len(state.get_valid_moves(opponent))
    score += (my_moves - opponent_moves) * MOBILITY_WEIGHT

    if game_phase == 'early':
        my_center_score = get_center_control_score(state, player)
        opponent_center_score = get_center_control_score(state, opponent)
        score += (my_center_score - opponent_center_score) * CENTER_WEIGHT

    if distance_diff > 2:
        score += 20.0

    if distance_diff < -2:
        score += my_path_count * 10.0

    return score


def get_game_phase(state: QuoridorGameState) -> str:
    """게임 진행 단계 판단"""
    total_walls_used = (
        (QuoridorGameState.INITIAL_WALLS - state.red_walls) +
        (QuoridorGameState.INITIAL_WALLS - state.blue_walls)
    )
    total_walls = QuoridorGameState.INITIAL_WALLS * 2

    red_dist = shortest_distance_to_goal(state, 'red')
    blue_dist = shortest_distance_to_goal(state, 'blue')
    total_dist = red_dist + blue_dist

    if total_walls_used < total_walls * 0.3 and total_dist > 10:
        return 'early'
    elif total_walls_used < total_walls * 0.7 or total_dist > 5:
        return 'mid'
    else:
        return 'late'


def get_center_control_score(state: QuoridorGameState, player: str) -> float:
    """중앙 장악도 점수 계산 (17x17 좌표계)"""
    y, x = state.get_player_position(player)
    center_y, center_x = 8, 8  # 17x17 보드의 중앙

    # 2칸씩 이동하므로 거리도 2로 나눔
    distance_from_center = (abs(y - center_y) + abs(x - center_x)) // 2
    score = max(0, 10 - distance_from_center * 2)

    return score


def is_winning_move(state: QuoridorGameState, player: str, move: Move) -> bool:
    """해당 수가 즉시 승리하는 수인지 확인"""
    if move.move_type == 'move':
        goal_line = state.get_goal_line(player)
        return move.y == goal_line
    return False


# ============================================================================
# SECTION 5: Minimax Algorithm
# ============================================================================

class MinimaxAI:
    """Minimax 알고리즘을 사용하는 Quoridor AI"""

    def __init__(self, player: str, difficulty: str = 'medium'):
        self.player = player
        self.difficulty = difficulty

        if difficulty == 'easy':
            self.max_depth = 2
            self.use_move_ordering = False
            self.max_wall_candidates = 10
        elif difficulty == 'medium':
            self.max_depth = 3
            self.use_move_ordering = True
            self.max_wall_candidates = 15
        else:  # hard
            self.max_depth = 4
            self.use_move_ordering = True
            self.max_wall_candidates = 20

        self.nodes_evaluated = 0
        self.max_time_per_move = 30.0
        self.cache_hits = 0
        self.cache_misses = 0

    def get_best_move(self, state: QuoridorGameState) -> Optional[Move]:
        """현재 상태에서 최선의 수 찾기"""
        self.nodes_evaluated = 0
        self.cache_hits = 0
        self.cache_misses = 0
        start_time = time.time()

        # 각 턴마다 캐시 초기화 (메모리 관리)
        _pathfinding_cache.clear()

        moves = generate_smart_moves(state, self.player, max_wall_moves=self.max_wall_candidates)

        if not moves:
            return None

        for move in moves:
            if is_winning_move(state, self.player, move):
                return move

        if self.use_move_ordering:
            moves = order_moves(state, self.player, moves)

        best_move = None
        best_score = float('-inf')
        alpha = float('-inf')
        beta = float('inf')

        for i, move in enumerate(moves):
            if time.time() - start_time > self.max_time_per_move:
                break

            new_state = apply_move(state, self.player, move)

            score = self.minimax(
                new_state,
                self.max_depth - 1,
                alpha,
                beta,
                False
            )

            if score > best_score:
                best_score = score
                best_move = move
                alpha = max(alpha, score)

        elapsed_time = time.time() - start_time
        print(f"[AI_PERFORMANCE] Nodes: {self.nodes_evaluated}, Time: {elapsed_time:.2f}s, "
              f"Cache hits: {self.cache_hits}, Cache misses: {self.cache_misses}")

        return best_move

    def minimax(
        self,
        state: QuoridorGameState,
        depth: int,
        alpha: float,
        beta: float,
        is_maximizing: bool
    ) -> float:
        """Minimax 알고리즘 with Alpha-Beta Pruning"""
        self.nodes_evaluated += 1

        if depth == 0 or state.is_goal('red') or state.is_goal('blue'):
            return evaluate_position(state, self.player, self.difficulty)

        current_player = self.player if is_maximizing else state.get_opponent(self.player)

        moves = generate_smart_moves(state, current_player, max_wall_moves=self.max_wall_candidates)

        if not moves:
            return evaluate_position(state, self.player, self.difficulty)

        if self.use_move_ordering and depth >= 2:
            moves = order_moves(state, current_player, moves)

        if is_maximizing:
            max_eval = float('-inf')

            for move in moves:
                new_state = apply_move(state, current_player, move)
                eval_score = self.minimax(new_state, depth - 1, alpha, beta, False)

                max_eval = max(max_eval, eval_score)
                alpha = max(alpha, eval_score)

                if beta <= alpha:
                    break

            return max_eval

        else:
            min_eval = float('inf')

            for move in moves:
                new_state = apply_move(state, current_player, move)
                eval_score = self.minimax(new_state, depth - 1, alpha, beta, True)

                min_eval = min(min_eval, eval_score)
                beta = min(beta, eval_score)

                if beta <= alpha:
                    break

            return min_eval


# ============================================================================
# SECTION 6: Main AI Interface
# ============================================================================

class QuoridorAI:
    """
    Quoridor AI 메인 클래스

    사용 예시:
        ai = QuoridorAI(player='blue', difficulty='medium')
        ai.apply_opponent_move('7,4')
        best_move = ai.get_best_move()
        print(best_move)
    """

    def __init__(self, player: str = 'blue', difficulty: str = 'medium'):
        """
        AI 초기화

        Args:
            player: AI가 플레이할 색상 ('red' 또는 'blue')
            difficulty: 난이도 ('easy', 'medium', 'hard')
        """
        self.player = player.lower()
        self.opponent = 'blue' if self.player == 'red' else 'red'
        self.difficulty = difficulty.lower()

        self.state = QuoridorGameState()
        self.ai_engine = MinimaxAI(self.player, self.difficulty)

    def reset(self):
        """게임 상태 초기화"""
        self.state = QuoridorGameState()

    def apply_opponent_move(self, move_str: str) -> bool:
        """
        상대방의 수를 적용

        Args:
            move_str:
                - 이동: "Y,X" (예: "7,4")
                - 벽: "wall:horizontal:Y:X" 또는 "wall:vertical:Y:X"
        """
        try:
            if move_str.startswith('wall:'):
                parts = move_str.split(':')
                if len(parts) != 4:
                    return False

                wall_type = parts[1]
                y = int(parts[2])
                x = int(parts[3])

                success = self.state.make_wall_move(self.opponent, wall_type, y, x)
                return success

            else:
                y, x = self.state.parse_position(move_str)
                success = self.state.make_move(self.opponent, y, x)
                return success

        except Exception as e:
            return False

    def get_best_move(self) -> Optional[Dict]:
        """
        AI의 최선의 수 계산

        Returns:
            - 이동: {'type': 'move', 'position': 'Y,X', 'y': Y, 'x': X}
            - 벽: {'type': 'wall', 'wall_type': 'horizontal/vertical', 'position': 'Y,X', 'y': Y, 'x': X}
        """
        best_move = self.ai_engine.get_best_move(self.state)

        if not best_move:
            return None

        if best_move.move_type == 'move':
            result = {
                'type': 'move',
                'position': f"{best_move.y},{best_move.x}",
                'y': best_move.y,
                'x': best_move.x
            }

        else:
            result = {
                'type': 'wall',
                'wall_type': best_move.wall_type,
                'position': f"{best_move.y},{best_move.x}",
                'y': best_move.y,
                'x': best_move.x
            }

        self._apply_ai_move(best_move)

        return result

    def _apply_ai_move(self, move: Move) -> bool:
        """AI의 수를 게임 상태에 적용"""
        if move.move_type == 'move':
            return self.state.make_move(self.player, move.y, move.x)
        else:
            return self.state.make_wall_move(self.player, move.wall_type, move.y, move.x)

    def get_game_state(self) -> Dict:
        """현재 게임 상태 정보 반환"""
        return {
            'red_position': f"{self.state.red_pos[0]},{self.state.red_pos[1]}",
            'blue_position': f"{self.state.blue_pos[0]},{self.state.blue_pos[1]}",
            'red_walls_remaining': self.state.red_walls,
            'blue_walls_remaining': self.state.blue_walls,
            'current_player': self.state.current_player,
            'red_distance_to_goal': shortest_distance_to_goal(self.state, 'red'),
            'blue_distance_to_goal': shortest_distance_to_goal(self.state, 'blue'),
            'is_game_over': self.state.is_goal('red') or self.state.is_goal('blue'),
            'winner': 'red' if self.state.is_goal('red') else ('blue' if self.state.is_goal('blue') else None)
        }

    def print_board(self):
        """보드 상태 출력 (디버깅용)"""
        print("\n" + "="*50)
        print(self.state)
        print("="*50 + "\n")

    def is_game_over(self) -> bool:
        """게임 종료 여부 확인"""
        return self.state.is_goal('red') or self.state.is_goal('blue')

    def get_winner(self) -> Optional[str]:
        """승자 반환"""
        if self.state.is_goal('red'):
            return 'red'
        elif self.state.is_goal('blue'):
            return 'blue'
        return None


# ============================================================================
# SECTION 7: Testing & Examples
# ============================================================================

def example_usage():
    """AI 사용 예시"""
    print("=" * 60)
    print("Quoridor AI Example Usage")
    print("=" * 60)

    ai = QuoridorAI(player='blue', difficulty='medium')
    ai.print_board()

    print("\n[Game] Red player moves to (7, 4)")
    ai.apply_opponent_move('7,4')
    ai.print_board()

    print("\n[Game] AI's turn")
    best_move = ai.get_best_move()
    print(f"[Game] AI chose: {best_move}")
    ai.print_board()

    game_state = ai.get_game_state()
    print("\n[Game State]")
    for key, value in game_state.items():
        print(f"  {key}: {value}")


def performance_test():
    """성능 테스트 - 캐싱 효과 확인"""
    print("=" * 80)
    print("AI Performance Test with Caching System")
    print("=" * 80)

    difficulties = ['easy', 'medium', 'hard']

    for difficulty in difficulties:
        print(f"\n{'='*80}")
        print(f"Testing {difficulty.upper()} difficulty")
        print(f"{'='*80}")

        ai = QuoridorAI(player='blue', difficulty=difficulty)

        # 몇 턴 시뮬레이션
        moves_to_test = 3
        total_time = 0

        for turn in range(moves_to_test):
            print(f"\n--- Turn {turn + 1} ---")

            start_time = time.time()
            best_move = ai.get_best_move()
            elapsed = time.time() - start_time
            total_time += elapsed

            if best_move:
                print(f"Move: {best_move}")
                print(f"Time: {elapsed:.2f}s")

                # 상대 수 시뮬레이션 (간단히 앞으로 이동)
                opponent = 'red'
                opponent_pos = ai.state.get_player_position(opponent)
                valid_moves = ai.state.get_valid_moves(opponent)
                if valid_moves:
                    # 목표에 가까워지는 수 선택
                    goal_line = ai.state.get_goal_line(opponent)
                    best_opp_move = min(valid_moves, key=lambda pos: abs(pos[0] - goal_line))
                    ai.state.make_move(opponent, best_opp_move[0], best_opp_move[1])
            else:
                print("No valid move found")
                break

        avg_time = total_time / moves_to_test if moves_to_test > 0 else 0
        print(f"\n{difficulty.upper()} Average time per move: {avg_time:.2f}s")
        print(f"Total time for {moves_to_test} moves: {total_time:.2f}s")


if __name__ == "__main__":
    import sys

    if len(sys.argv) > 1 and sys.argv[1] == 'test':
        performance_test()
    else:
        example_usage()
