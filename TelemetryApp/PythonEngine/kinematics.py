import numpy as np
from scipy.signal import savgol_filter

def apply_savgol_filter(signal_array, window_length=21, polyorder=2):
    """
    Wygładza sygnał 1D przy użyciu filtru Savitzky'ego-Golaya.
    Parametry domyślne (21, 2) wzięte bezpośrednio z konfiguracji MATLABa (cfg.sg_frame, cfg.sg_poly).
    """
    if len(signal_array) < window_length:
        return signal_array # Zabezpieczenie przed zbyt krótkim sygnałem
    
    # Wyłapanie i zignorowanie wartości NaN (jeśli występują w symulowanych danych)
    valid_idx = ~np.isnan(signal_array)
    filtered = np.copy(signal_array)
    
    if np.any(valid_idx):
        filtered[valid_idx] = savgol_filter(signal_array[valid_idx], window_length, polyorder)
        
    return filtered

def calculate_angular_acceleration(gyro_array_rad, dt):
    """
    Oblicza przyspieszenie kątowe (alfa) jako pochodną prędkości kątowej (omega).
    gyro_array_rad: Macierz [N, 3] z [RollRate, PitchRate, YawRate] w rad/s
    dt: Krok czasowy w sekundach (lub wektor kroków dt)
    """
    # Używamy np.gradient, co jest odpowiednikiem MATLABowego diff() / dt, 
    # ale zachowuje długość tablicy (liczy różnice centralne).
    return np.gradient(gyro_array_rad, dt, axis=0)

def transform_to_cg(acc_imu, gyro_rad, dt, r_vec):
    """
    Przelicza przyspieszenia z pozycji czujnika IMU do środka ciężkości (CG).
    
    acc_imu: Macierz [N, 3] przyspieszeń [ax, ay, az] w m/s^2
    gyro_rad: Macierz [N, 3] prędkości kątowych [wx, wy, wz] w rad/s
    dt: Średni krok czasowy pomiarów (np. 0.01 dla 100Hz)
    r_vec: Wektor przesunięcia [x, y, z] od CG do IMU w metrach
    
    Zwraca: Macierz [N, 3] przyspieszeń w środku ciężkości
    """
    # 1. Sprawdzenie, czy r_vec to same zera. Jeśli tak, oszczędzamy CPU i pomijamy macierze.
    if np.allclose(r_vec, 0.0):
        return acc_imu

    N = acc_imu.shape[0]
    
    # 2. Obliczenie przyspieszeń kątowych (alfa)
    alfa_rad = calculate_angular_acceleration(gyro_rad, dt)
    
    # 3. Rozszerzenie wektora przesunięcia do pełnej macierzy [N, 3], by móc mnożyć wektory
    R = np.tile(r_vec, (N, 1))
    
    # 4. Obliczenie członów równania
    # alfa x R
    term1 = np.cross(alfa_rad, R)
    
    # omega x (omega x R)
    omega_cross_R = np.cross(gyro_rad, R)
    term2 = np.cross(gyro_rad, omega_cross_R)
    
    # 5. Równanie końcowe
    acc_cg = acc_imu - term1 - term2
    
    return acc_cg