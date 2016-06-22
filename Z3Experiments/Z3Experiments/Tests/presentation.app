//////////////////////////////////////////////////////////////////////	
// Presentation gestures
//////////////////////////////////////////////////////////////////////

APP present:
	GESTURE next_360:

		POSE pose1:
		rotate your right wrist 15 degrees up,
		rotate your right wrist 15 degrees to your right.
		
		POSE pose2:
		rotate your right wrist 25 degrees up.
		
		POSE pose3:
		rotate your right wrist 45 degrees up.

		EXECUTION:
		pose1,
		pose2,
		pose3.
		
	
//APP present:
//	GESTURE next_360:
//
//		POSE main:
//		put your right wrist to the left of your right shoulder,
//		put your right wrist below your right elbow,
//		put your right wrist in front of your right shoulder.
//		
//		POSE round_next_up:
//		rotate your right wrist 15 degrees to your back,
//		rotate your right wrist 15 degrees up.
//		
//		POSE round_next_down:
//		rotate your right wrist 30 degrees to your front,
//		rotate your right wrist 15 degrees down.
//
//		EXECUTION:
//		main,
//		round_next_up,
//		main,
//		round_next_down.
//
//	GESTURE prev_360:
//
//		POSE round_prev_up:
//		rotate your right wrist 30 degrees to your front,
//		rotate your right wrist 30 degrees up.
//		
//		POSE round_prev_down:
//		rotate your right wrist 60 degrees to your back,
//		rotate your right wrist 30 degrees down.
//
//		EXECUTION:
//		main,
//		round_prev_up,
//		main,
//		round_prev_down.