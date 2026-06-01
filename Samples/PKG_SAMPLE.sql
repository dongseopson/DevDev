CREATE OR REPLACE PACKAGE BODY PKG_SAMPLE AS
  -- ============================================================
  -- 샘플 패키지 : PL/SQL Analyzer 테스트용
  -- ============================================================

  PROCEDURE GET_EMPLOYEE_INFO(
    p_emp_id      IN   NUMBER,
    p_dept_id     IN   NUMBER       DEFAULT NULL,
    p_result      OUT  VARCHAR2,
    p_err_msg     OUT  VARCHAR2,
    p_status_code OUT  NUMBER
  ) IS
    v_emp_name    VARCHAR2(100);
    v_salary      NUMBER;
    v_dept_name   VARCHAR2(50);
    v_hire_date   DATE;
    v_unused_var  VARCHAR2(10);    -- 미사용 변수 예시
    ex_not_found  EXCEPTION;
  BEGIN
    -- 직원 기본 정보 조회
    SELECT e.emp_name,
           e.salary,
           (SELECT d.dept_name
              FROM departments d
             WHERE d.dept_id = e.dept_id),
           e.hire_date
      INTO v_emp_name, v_salary, v_dept_name, v_hire_date
      FROM employees   e
     WHERE e.emp_id = p_emp_id
       AND e.status = 'A';

    IF v_emp_name IS NULL THEN
      RAISE ex_not_found;
    END IF;

    -- 부서별 집계 조회 (FROM 절 서브쿼리)
    SELECT dept_summary.avg_sal,
           dept_summary.emp_count
      INTO v_salary, p_status_code
      FROM (SELECT AVG(salary)  AS avg_sal,
                   COUNT(*)     AS emp_count
              FROM employees
             WHERE dept_id = p_dept_id) dept_summary;

    p_result      := v_emp_name || ' | ' || v_dept_name || ' | ' || TO_CHAR(v_salary,'999,999');
    p_status_code := 0;
    p_err_msg     := NULL;

    -- 감사 로그 INSERT
    INSERT INTO audit_log (log_id, proc_name, param_val, log_dt)
    VALUES (seq_audit.NEXTVAL, 'GET_EMPLOYEE_INFO', TO_CHAR(p_emp_id), SYSDATE);

    COMMIT;

    -- 다른 패키지 함수 호출
    PKG_NOTIFY.SEND_ALERT(p_emp_id, 'QUERY');
    UTIL_LOG.WRITE_LOG('GET_EMPLOYEE_INFO', p_emp_id);

  EXCEPTION
    WHEN ex_not_found THEN
      p_status_code := -1;
      p_err_msg     := '직원을 찾을 수 없습니다: ' || p_emp_id;
      ROLLBACK;
    WHEN OTHERS THEN
      p_status_code := -99;
      p_err_msg     := SQLERRM;
      ROLLBACK;
  END GET_EMPLOYEE_INFO;


  FUNCTION CALC_BONUS(
    p_emp_id    IN NUMBER,
    p_year      IN NUMBER DEFAULT EXTRACT(YEAR FROM SYSDATE),
    p_rate      IN NUMBER DEFAULT 0.1
  ) RETURN NUMBER IS
    v_base_sal  NUMBER;
    v_perf_score NUMBER;
    v_bonus     NUMBER := 0;
  BEGIN
    -- WHERE 절 서브쿼리 포함 예시
    SELECT base_salary
      INTO v_base_sal
      FROM employees
     WHERE emp_id = p_emp_id
       AND dept_id IN (SELECT dept_id
                         FROM departments
                        WHERE use_yn = 'Y'
                          AND budget > (SELECT AVG(budget) FROM departments));

    -- 성과 점수 조회
    SELECT NVL(AVG(score), 0)
      INTO v_perf_score
      FROM performance_reviews
     WHERE emp_id   = p_emp_id
       AND rev_year = p_year;

    IF v_perf_score >= 90 THEN
      v_bonus := v_base_sal * p_rate * 2;
    ELSIF v_perf_score >= 70 THEN
      v_bonus := v_base_sal * p_rate;
    ELSE
      v_bonus := v_base_sal * p_rate * 0.5;
    END IF;

    -- UPDATE 예시
    UPDATE employee_bonus
       SET bonus_amt    = v_bonus,
           calc_date    = SYSDATE,
           calc_year    = p_year
     WHERE emp_id = p_emp_id
       AND bonus_year = p_year;

    RETURN v_bonus;

  EXCEPTION
    WHEN NO_DATA_FOUND THEN RETURN 0;
    WHEN OTHERS THEN RAISE;
  END CALC_BONUS;


  PROCEDURE SYNC_DEPT_STATS(
    p_dept_id  IN NUMBER,
    p_force    IN VARCHAR2 DEFAULT 'N'
  ) IS
    CURSOR cur_emp (c_dept IN NUMBER) IS
      SELECT emp_id, emp_name, salary, hire_date
        FROM employees
       WHERE dept_id = c_dept
         AND status  = 'A'
       ORDER BY hire_date;
    v_total_sal  NUMBER := 0;
    v_emp_cnt    NUMBER := 0;
  BEGIN
    FOR rec IN cur_emp(p_dept_id) LOOP
      v_total_sal := v_total_sal + rec.salary;
      v_emp_cnt   := v_emp_cnt + 1;

      -- MERGE 예시
      MERGE INTO emp_stats t
      USING (SELECT rec.emp_id AS eid, rec.salary AS sal FROM DUAL) s
         ON (t.emp_id = s.eid)
       WHEN MATCHED THEN
         UPDATE SET t.last_salary = s.sal, t.upd_dt = SYSDATE
       WHEN NOT MATCHED THEN
         INSERT (emp_id, last_salary, crt_dt)
         VALUES (s.eid, s.sal, SYSDATE);
    END LOOP;

    -- 부서 통계 갱신
    UPDATE department_stats
       SET total_salary  = v_total_sal,
           emp_count     = v_emp_cnt,
           last_sync_dt  = SYSDATE
     WHERE dept_id = p_dept_id;

    COMMIT;
  EXCEPTION
    WHEN OTHERS THEN
      ROLLBACK;
      RAISE_APPLICATION_ERROR(-20001, 'SYNC_DEPT_STATS 오류: ' || SQLERRM);
  END SYNC_DEPT_STATS;


  -- Dead Code 예시 (이 함수는 아무데서도 호출하지 않음)
  FUNCTION UNUSED_HELPER(p_val IN NUMBER) RETURN VARCHAR2 IS
  BEGIN
    RETURN TO_CHAR(p_val);
  END UNUSED_HELPER;

END PKG_SAMPLE;