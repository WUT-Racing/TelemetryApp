import numpy as np

def latlon_to_xy(lat_array, lon_array, lat0, lon0):
    """
    Konwertuje współrzędne GPS (szerokość, długość) na płaską siatkę X, Y (w metrach).
    (lat0, lon0) to punkt referencyjny, zazwyczaj pierwszy punkt z logu dla danego przejazdu.
    Zwraca: (x_array, y_array)
    """
    # Stałe przeliczeniowe obwodu Ziemi (odpowiednik z MATLABa)
    m_per_deg_lat = 111320.0
    
    # Długość stopnia na równiku maleje wraz z szerokością geograficzną
    m_per_deg_lon = 111320.0 * np.cos(np.radians(lat0))
    
    x_array = (lon_array - lon0) * m_per_deg_lon
    y_array = (lat_array - lat0) * m_per_deg_lat
    
    return x_array, y_array


def fit_circle_kasa(x, y):
    """
    Dopasowuje okrąg do punktów (x, y) metodą Kasy (Least Squares).
    Bardzo szybka, nieiteracyjna metoda algebraiczna.
    
    Zwraca: (xc, yc, R) - współrzędne środka (xc, yc) i promień (R).
    """
    # Równanie Kasy: 2*a*X + 2*b*Y - c = X^2 + Y^2
    Z = x**2 + y**2
    
    # Tworzymy macierz A o wymiarach [N x 3]
    A = np.column_stack([x, y, np.ones(len(x))])
    
    # Rozwiązanie układu równań metodą najmniejszych kwadratów w NumPy
    # To jest pythonowy odpowiednik operatora ' backslash ' (\) z MATLABa.
    res, residuals, rank, s = np.linalg.lstsq(A, Z, rcond=None)
    
    p1, p2, p3 = res
    
    # Wyciągnięcie parametrów fizycznych okręgu
    xc = p1 / 2.0
    yc = p2 / 2.0
    R = np.sqrt(xc**2 + yc**2 + p3)
    
    return xc, yc, R


def calculate_heading_angle(x_array, y_array, xc, yc):
    """
    Oblicza ciągły kąt obrotu samochodu wokół wirtualnego środka okręgu.
    Niezbędne do zliczania pętli Skidpada (sprawdzanie, czy kąt przekroczył 360, 720 stopni).
    """
    # arctan2 zwraca kąty z przedziału [-pi, pi]
    theta_rad = np.arctan2(y_array - yc, x_array - xc)
    
    # UNWRAP: To super ważna funkcja (odpowiednik unwrap() w MATLABie). 
    # Niweluje "skoki" przy przejściu z 359 stopni na 0, tworząc ciągły sygnał 
    # np. 0 -> 360 -> 720 -> 1080 itd., co pozwala łatwo zliczać pętle.
    theta_rad_unwrapped = np.unwrap(theta_rad)
    
    # Konwersja na stopnie
    theta_deg = np.degrees(theta_rad_unwrapped)
    
    # "Zerowanie" kąta na początku (żeby start pętli był zawsze od 0 stopni)
    theta_deg = theta_deg - theta_deg[0]
    
    return theta_deg


def find_skidpad_center_and_start_line(xc_left, yc_left, xc_right, yc_right):
    """
    Znajduje wirtualną linię startu/mety pomiędzy dwoma pętlami Skidpada.
    Zwraca punkt środkowy trasy (mid_x, mid_y) oraz znormalizowany wektor prostopadły,
    który wyznacza wirtualną bramkę z fotokomórką.
    """
    # 1. Główny środek toru (pomiędzy lewą a prawą pętlą)
    mid_x = (xc_left + xc_right) / 2.0
    mid_y = (yc_left + yc_right) / 2.0
    
    # 2. Wektor od środka lewego do środka prawego okręgu
    dx = xc_right - xc_left
    dy = yc_right - yc_left
    
    # 3. Kąt prostopadły (obrót wektora o 90 stopni). 
    # To definiuje linię, przez którą bolid wjeżdża z prostej startowej.
    dir_x = -dy
    dir_y = dx
    
    # 4. Normalizacja wektora (żeby miał długość 1.0)
    length = np.sqrt(dir_x**2 + dir_y**2)
    if length > 0:
        dir_x /= length
        dir_y /= length
        
    return (mid_x, mid_y), (dir_x, dir_y)